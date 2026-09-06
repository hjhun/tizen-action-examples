#!/usr/bin/env python3
"""Check installation metadata and Action bindings; requires python3-jsonschema."""
import argparse
import copy
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

from jsonschema import Draft202012Validator

APP = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--catalog', type=Path, default=APP.parent.parent / 'appfw/tizen-action/default-actions')
parser.add_argument('--validator-source', type=Path,
                    help='Matching platform src/common/action_validator.cc; defaults to the catalog sibling source')
args = parser.parse_args()
validator_source = args.validator_source or args.catalog.parent / 'src/common/action_validator.cc'
match = re.search(r'constexpr const char kMetaSchemaForActionSchema\[\] = R"__schema\((.*?)\)__schema";',
                  validator_source.read_text(), re.S)
assert match, f'Action installation meta-schema not found in {validator_source}'
meta_schema = json.loads(match.group(1))
Draft202012Validator.check_schema(meta_schema)
metadata_validator = Draft202012Validator(meta_schema)


def error_tree(error):
    yield error
    for child in error.context:
        yield from error_tree(child)


# Validate only app-owned authored Actions against the actual installation contract.
# Entity references have their own branch; array items are not propertyDef nodes.
authored_actions = {path.name: json.loads(path.read_text()) for path in sorted((APP / 'actions').glob('*.action'))}
metadata_errors = []
for name, definition in authored_actions.items():
    errors = list(metadata_validator.iter_errors(definition))
    missing_descriptions = sorted({
        '/'.join(map(str, error.absolute_path))
        for root in errors for error in error_tree(root)
        if error.validator == 'required' and 'description' in error.validator_value
        and isinstance(error.instance, dict) and 'description' not in error.instance
    })
    metadata_errors.extend(f'{name}/{path}: description is required by the primitive propertyDef branch'
                           for path in missing_descriptions)
    if errors and not missing_descriptions:
        metadata_errors.append(f'{name}: installation meta-schema rejected metadata ({errors[0].validator})')
if metadata_errors:
    raise SystemExit('\n'.join(metadata_errors))

# Removing any one of the three installation-blocking descriptions must regress.
for name, field in [
    ('App_Tizen.Action.CalendarCustom_GetEventByIds.action', 'result'),
    ('App_Tizen.Action.CalendarCustom_GetEventByIds.action', 'unresolvedIds'),
    ('App_Tizen.Action.CalendarCustom_SearchInPeriod.action', 'result'),
]:
    missing = copy.deepcopy(authored_actions[name])
    del missing['outputSchema']['properties'][field]['description']
    assert not metadata_validator.is_valid(missing), (name, field, 'missing description accepted')

property_validator = Draft202012Validator({
    '$ref': '#/definitions/propertyDef', 'definitions': meta_schema['definitions'],
})
for primitive in ['string', 'integer', 'boolean', 'number', 'object', 'array']:
    assert not property_validator.is_valid({'type': primitive}), primitive
    assert property_validator.is_valid({'type': primitive, 'description': 'Field value'}), primitive
for entity_type in ['Tizen.Entity.Status', 'Tizen.Entity.CalendarEvent', 'Calendar.Entity.SearchQuery']:
    assert property_validator.is_valid({'type': entity_type}), entity_type
assert property_validator.is_valid({'type': 'array', 'description': 'Identifiers', 'items': {'type': 'string'}})
print('Authored metadata: 2 Actions pass platform meta-schema; three missing-description regressions rejected; Entity/item boundaries preserved')

manifest = ET.parse(APP / 'src/Calendar.App/tizen-manifest.xml')
ns = {'t': 'http://tizen.org/ns/packages'}
app = manifest.find('t:ui-application', ns)
assert app.attrib['type'] == 'dotnet' and app.attrib['api-version'] == '14'
metadata = [(m.attrib['key'], m.attrib['value']) for m in app.findall('t:metadata', ns)]
prefix = 'http://tizen.org/metadata/action'
providers = [v for k, v in metadata if k == prefix + '/provider']
assert len(providers) == len(set(providers))

seq = {}
category = None
for line in (args.catalog / 'action.seq').read_text().splitlines():
    line = line.strip()
    if line.startswith('['): category = line[1:-1]; seq[category] = []
    elif line and not line.startswith('#'): seq[category].append(line)

bindings = [
    ('Calendar', 'Calendar.ActionProvider', 'CalendarActionProvider'),
    ('Reminder', 'Calendar.ScheduleActionProvider', 'ScheduleReminderActionProvider'),
    ('View', 'Calendar.ViewActionProvider', 'CalendarViewActionProvider'),
    ('CalendarCustom', 'Calendar.ActionProvider', 'CalendarCustomActionProvider'),
]
expected_providers = []
for category, project, binding in bindings:
    if category == 'CalendarCustom':
        actions = ['App_Tizen.Action.CalendarCustom_GetEventByIds', 'App_Tizen.Action.CalendarCustom_SearchInPeriod']
        schema_dir = APP / 'actions'
    else:
        actions = seq['Tizen.Action.' + category]
        schema_dir = args.catalog / 'actions'
    expected_providers.extend(actions)
    generated = (APP / 'src' / project / 'Generated' / (binding + '.cs')).read_text()
    enum = re.search(r'private enum MethodId\s*:\s*int\s*\{(.*?)\}', generated, re.S).group(1)
    actual = re.findall(r'^\s*(\w+)\s*=\s*(-?\d+)', enum, re.M)
    methods = [(name, int(number)) for name, number in actual if not name.startswith('__')]
    assert [name for name, _ in methods] == [a.rsplit('_', 1)[1] for a in actions], (category, methods)
    # Result and callback occupy 0/1; category entries begin at method 2.
    assert dict(actual)['__Result'] == '0' and dict(actual)['__Callback'] == '1'
    assert [number for _, number in methods] == list(range(2, len(actions) + 2)), (category, methods)
    for action in actions:
        schema = json.loads((schema_dir / (action + '.action')).read_text())
        assert schema['name'] == action and schema['category'] == 'Tizen.Action.' + category
        if category == 'CalendarCustom':
            assert (prefix, action + '.action') in metadata
            assert schema['details']['appid'] == app.attrib['appid']
    print(f'{category}: schema and {len(actions)} positional methods match')
assert set(providers) == set(expected_providers)
for entity in (APP / 'entities').glob('*.entity'):
    assert (prefix + '/entity', entity.name) in metadata
    definition = json.loads(entity.read_text())
    assert (args.catalog / 'entities' / (definition['base'] + '.entity')).is_file()
print('Manifest: 16 providers, custom definitions/entities, and .NET API14 match')
