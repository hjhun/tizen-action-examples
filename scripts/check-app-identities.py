#!/usr/bin/env python3
"""Check canonical package/application IDs and app-owned Action metadata."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
errors = []
manifests = sorted(root.glob('*/src/*.App/tizen-manifest.xml'))
for manifest in manifests:
    app = manifest.relative_to(root).parts[0]
    expected = 'org.tizen.' + app.lower()
    tree = ET.parse(manifest).getroot()
    applications = [node for node in tree if node.tag.endswith('application')]
    if tree.get('package') != expected or not applications or any(node.get('appid') != expected for node in applications):
        errors.append(f'{app}: package and application IDs must be {expected}')
    for action in sorted((root / app / 'actions').glob('App_*.action')):
        if json.loads(action.read_text())['details']['appid'] != expected:
            errors.append(f'{action.relative_to(root)}: details.appid must be {expected}')
if errors:
    raise SystemExit('\n'.join(errors))
assert manifests, 'No app manifests found'
print(f'PASS: {len(manifests)} canonical app manifests and app-owned Action IDs')
