#!/usr/bin/env python3
"""Compare unmodified actionc output and advertised P1 contracts against the platform."""
import os
import re
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

APP = Path(__file__).resolve().parents[1]
CATALOG = Path(os.environ.get('ACTIONC_DATA_DIR', APP.parent.parent / 'appfw/tizen-action/default-actions'))
expected = {'Tv_Tizen.Action.Browser_' + name for name in ('GetCurrentPage', 'GetTabs', 'ToPresentation')}
expected |= {'Common_Tizen.Action.View_' + name for name in ('FindById', 'GetAnnotatedViews', 'GetFocusedView', 'ToPresentation')}
expected.add('App_Tizen.Action.BrowserCustom_GetPageByIds')
failures = []
ns = {'t': 'http://tizen.org/ns/packages'}
app = ET.parse(APP / 'src/Browser.App/tizen-manifest.xml').find('t:ui-application', ns)
actual = [m.attrib['value'] for m in app.findall('t:metadata', ns) if m.attrib['key'].endswith('/action/provider')]
if set(actual) != expected or len(actual) != 8:
    failures.append(f'advertised actions: missing={expected-set(actual)}, unexpected={set(actual)-expected}')
if not any(m.attrib['key'] == 'http://tizen.org/metadata/action' and m.attrib['value'] == 'App_Tizen.Action.BrowserCustom_GetPageByIds.action' for m in app.findall('t:metadata', ns)):
    failures.append('custom schema installation metadata missing')
with tempfile.TemporaryDirectory(prefix='browser-contract-') as directory:
    for category, name, relative in [
        ('Browser', 'TizenActionBrowser', 'src/Browser.ActionProvider/TizenActionBrowserGenerated.cs'),
        ('View', 'TizenActionView', 'src/Browser.ViewActionProvider/TizenActionView.cs'),
        ('BrowserCustom', 'BrowserCustomActions', 'src/Browser.ActionProvider/Generated/BrowserCustomActions.cs'),
    ]:
        output = Path(directory) / name
        command = [os.environ.get('ACTIONC_BIN', 'actionc'), '-d', str(CATALOG), '-l', 'C#', '-o', str(output)]
        if category == 'BrowserCustom':
            schema = APP / 'actions/App_Tizen.Action.BrowserCustom_GetPageByIds.action'
            if not schema.exists():
                failures.append('custom resolver schema/generated missing')
                continue
            command += ['-i', str(schema)]
        else:
            command += ['-a', 'Tizen.Action.' + category]
        run = subprocess.run(command, capture_output=True, text=True)
        raw = output.with_suffix('.cs')
        if run.returncode or not raw.exists():
            raise SystemExit(run.stdout + run.stderr)
        source = raw.read_text()
        methods = re.search(r'private enum MethodId : int\s*\{(.*?)\}', source, re.S).group(1)
        actual_slots = {name: int(slot) for name, slot in re.findall(r'(\w+) = (\d+)', methods) if not name.startswith('__')}
        if category == 'BrowserCustom':
            expected_slots = {'GetPageByIds': 2}
        else:
            section = re.search(r'^\[Tizen\.Action\.' + category + r'\]\s*$(.*?)(?=^\[|\Z)', (CATALOG / 'action.seq').read_text(), re.M | re.S).group(1)
            names = [line.strip().rsplit('_', 1)[1] for line in section.splitlines() if line.strip() and not line.lstrip().startswith('#')]
            expected_slots = {name: slot for slot, name in enumerate(names, 2)}
        assert actual_slots == expected_slots, (category, actual_slots, expected_slots)
        print(category + ': slots ' + str(actual_slots))
        tracked = APP / relative
        if not tracked.exists() or raw.read_bytes() != tracked.read_bytes():
            failures.append(category + ': not byte-identical to full raw actionc output (includes slot/wire contract)')
        else:
            print(category + ': full raw actionc byte equality PASS')
if failures:
    raise SystemExit('\n'.join(failures))
print('PASS: complete generated categories and exactly eight advertised P1 actions')
