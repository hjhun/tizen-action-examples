#!/usr/bin/env python3
"""Check installed Calendar/Reminder View contracts through action-tool.

Foreground the app and focus a control with Aurum before running this check.
This script performs read-only provider calls; --display additionally renders
the actual returned Presentation in the installed reference renderer.
"""
import argparse
import json
import math
from pathlib import Path
import shlex
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--app', required=True, choices=['calendar', 'reminder'])
    parser.add_argument('--action-tool', default='action-tool')
    parser.add_argument('--iterations', type=int, default=20)
    parser.add_argument('--display', action='store_true')
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if not 1 <= args.iterations <= 100:
        parser.error('--iterations must be between 1 and 100')
    appid = 'org.tizen.actionexamples.' + args.app
    checks = []

    def call(name, arguments, app=appid):
        request = {'id': 1, 'params': {'name': name, 'appid': app, 'arguments': arguments}}
        command = shlex.quote(args.action_tool) + ' execute --json ' + shlex.quote(json.dumps(request))
        result = subprocess.run(['sdb', '-s', args.serial, 'shell', command],
                                text=True, capture_output=True, timeout=30, check=True)
        wire = json.loads(result.stdout)
        assert 'error' not in wire, wire
        body = wire['result']
        assert not body.get('isError', False), body
        return body.get('structuredContent') or json.loads(body['content'][0]['text'])

    def view(method, arguments=None):
        return call('Common_Tizen.Action.View_' + method, arguments or {})

    for iteration in range(args.iterations):
        visible = view('GetAnnotatedViews')
        assert visible['return']['Success'] and visible['views'], visible
        ids = [v['Id'] for v in visible['views']]
        assert len(ids) == len(set(ids)), 'Duplicate visible IDs'
        for item in visible['views']:
            bounds = item['ScreenBounds']
            assert all(math.isfinite(bounds[k]) for k in ('X', 'Y', 'Width', 'Height'))
            assert bounds['Width'] > 0 and bounds['Height'] > 0, item
        page = visible['views'][0]
        found = view('FindById', {'id': page['Id']})
        assert found['return']['Success'], (iteration, found)
        current = found['view']
        assert current['Id'] == page['Id']
        checks.append('consecutive visible/find ' + str(iteration + 1))

    focus = view('GetFocusedView')
    assert focus['return']['Success'] and focus['view']['IsFocused'], focus
    assert focus['view']['Id'] in [v['Id'] for v in view('GetAnnotatedViews')['views']]
    checks.append('focused view belongs to current frame')
    assert not view('FindById', {'id': 'provider-validation-missing-view'})['return']['Success']
    assert not view('FindById', {'id': 'x' * 1025})['return']['Success']
    checks.append('missing and oversized ID typed failures')
    annotation = current['Annotation']
    bad = {'Id': current['Id'], 'Annotation': {**annotation, 'EntityId': 'mismatched'}}
    assert not view('ToPresentation', bad)['return']['Success']
    checks.append('mismatched view/entity identity typed failure')
    stale = {'Id': current['Id'], 'Annotation': {**annotation, 'EntityInfo': 'untrusted stale content'}}
    presentation = view('ToPresentation', stale)
    assert presentation['return']['Success'], presentation
    payload = presentation['result']
    assert 'untrusted stale content' not in payload['Document']
    assert json.loads(payload['Template'])['surfaceUpdate']['components']
    assert json.loads(payload['Document'])['dataModelUpdate']['value']
    checks.append('authoritative current-state legacy v0.8 Presentation')
    if args.display:
        status = call('Tv_Tizen.Action.Presentation_Show', payload, 'org.tizen.displaypresentation')
        assert status['Success'], status
        checks.append('actual View-to-Display round trip')
    report = {'passed': True, 'serial': args.serial, 'appid': appid, 'checks': checks,
              'page': current, 'focus': focus['view'], 'presentation': payload}
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(f'{appid}: PASS ({len(checks)} checks); {args.output}')


if __name__ == '__main__':
    main()
