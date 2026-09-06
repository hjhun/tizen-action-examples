#!/usr/bin/env python3
"""Verify mutation query postconditions in action-tool's saved JSON report.

Usage: check_target_results.py <action-tool-report.json> <scenario-dir/run.json>
Only IDs owned by this scenario are compared; unrelated target data is preserved.
"""
import json
from pathlib import Path
import sys

if len(sys.argv) != 3:
    raise SystemExit(__doc__)
report, fixture = (json.loads(Path(p).read_text()) for p in sys.argv[1:])
assert report['passed'], 'action-tool scenario failed'
assert len(report['steps']) == 33
assert all(step['status'] == 'ok' for step in report['steps'])
rows = {step['label']: step['result']['result'] for step in report['steps']}
run = fixture['run']
owned = {item['Id'] for item in fixture['reservations']}

for label, expected in [
    ('Read reservation after viewing', {run + '-viewing'}),
    ('Read reservation after recording', owned),
    ('Read reservations after cancel Viewing', {run + '-recording'}),
    ('Read reservations after cancel Recording', set()),
]:
    body = rows[label]['structuredContent']
    assert body['return']['Success']
    actual = {item['Id'] for item in body['result']} & owned
    assert actual == expected, (label, actual, expected)

for state in ['To-do', 'In-progress', 'Blocked', 'Done']:
    items = rows['Read persisted ' + state]['structuredContent']['result']
    assert len(items) == 1 and items[0]['Id'] == run
    assert items[0]['State']['State'] == state

presentation = rows['Present current state despite stale caller']['structuredContent']['result']
value = json.loads(presentation['Document'])['dataModelUpdate']['value']
assert value['state'] == 'Done' and value['note'] == 'Updated note'
assert rows['Read deletion postcondition']['structuredContent']['result'] == []
print('PASS: 33 Action steps, 4 reservation postconditions, 4 persisted states, current presentation, deletion')
