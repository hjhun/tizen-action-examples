#!/usr/bin/env python3
"""Compare the complete current categories, metadata and regenerated source."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

APP = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--catalog', type=Path, default=APP.parent.parent / 'appfw/tizen-action/default-actions')
args = parser.parse_args()
categories = {}
for line in (args.catalog / 'action.seq').read_text().splitlines():
    line = line.strip()
    if line.startswith('['):
        category = line[1:-1]
        categories[category] = []
    elif line and not line.startswith('#'): categories[category].append(line)
manifest = ET.parse(APP / 'src/DisplayPresentation.App/tizen-manifest.xml')
ns = {'t': 'http://tizen.org/ns/packages'}
app = manifest.find('t:ui-application', ns)
assert app.attrib['api-version'] == '14' and app.attrib['appid'] == 'org.tizen.displaypresentation'
advertised = [m.attrib['value'] for m in app.findall('t:metadata', ns)
              if m.attrib['key'] == 'http://tizen.org/metadata/action/provider']
expected = categories['Tizen.Action.Presentation'] + categories['Tizen.Action.View']
assert sorted(advertised) == sorted(expected)
with tempfile.TemporaryDirectory(prefix='presentation-bindings-check-') as tmp:
    subprocess.run(['python3', str(APP / 'generate-bindings.py'), '--catalog', str(args.catalog), '--output-root', tmp], check=True)
    for category, project, name in [('Presentation', 'ActionProvider', 'DisplayActions'), ('View', 'ViewActionProvider', 'ViewActions')]:
        relative = Path('src') / ('DisplayPresentation.' + project) / 'Generated' / (name + '.cs')
        generated = APP / relative
        assert generated.read_bytes() == (Path(tmp) / relative).read_bytes(), relative
        enum = re.search(r'private enum MethodId\s*:\s*int\s*\{(.*?)\}', generated.read_text(), re.S).group(1)
        methods = [(n, int(i)) for n, i in re.findall(r'^\s*(\w+)\s*=\s*(-?\d+)', enum, re.M) if not n.startswith('__')]
        assert methods == [(action.rsplit('_', 1)[1], i + 2) for i, action in enumerate(categories['Tizen.Action.' + category])]
        print(category + ': complete method order and regeneration match')
print('Manifest: current Presentation/View, five providers, API 14')
