#!/usr/bin/env python3
"""Measure a foreground Gallery through Aurum and Action/View RPCs.

Prepare Pictures with real media first. Screenshots and input acceptance are
separate gates; this script does not dismiss platform dialogs or invent bounds.
"""
import argparse
import copy
import json
import math
from pathlib import Path
from target_support import Target

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--serial', default='emulator-26111')
p.add_argument('--port', type=int, default=55061)
p.add_argument('--tool', default='action-tool')
p.add_argument('--width', type=int, required=True)
p.add_argument('--height', type=int, required=True)
p.add_argument('--report', type=Path, required=True)
a = p.parse_args()
t = Target(a.serial, a.port, a.tool)
checks = []

def check(value, label):
    assert value, label
    checks.append(label)

def ok(value):
    return value.get('return', value).get('Success') is True

health = t.ui('health')
check((health['width'], health['height']) == (a.width, a.height), 'native Aurum size')
result = t.view('GetAnnotatedViews')
check(ok(result), 'foreground annotated views')
views = result['views']
page = views[0]['ScreenBounds']
scale = min(a.width / 1920, a.height / 1080)
check(abs(page['Width'] - 1920 * scale) < 1, 'measured canvas width')
check(abs(page['Height'] - 1080 * scale) < 1, 'measured canvas height')
check(abs(page['X'] - (a.width - 1920 * scale) / 2) < 1, 'centered canvas x')
check(abs(page['Y'] - (a.height - 1080 * scale) / 2) < 1, 'centered canvas y')
photo = next(v for v in views if v['Annotation']['EntityType'] == 'Tizen.Entity.Photo')
check(abs(photo['ScreenBounds']['Width'] - 433 * scale) < 12 * scale, 'photo width scaled once with focus tolerance')
for view in views:
    bounds = view['ScreenBounds']
    check(all(math.isfinite(x) for x in bounds.values()) and bounds['Width'] > 0 and bounds['Height'] > 0, 'finite positive measured view')
    found = t.view('FindById', {'id': view['Id']})
    check(ok(found) and found['view']['ScreenBounds'] == bounds, 'FindById preserves native bounds')
    check(ok(t.view('ToPresentation', view)), 'current View_ToPresentation')
for _ in range(20):
    current = t.view('GetAnnotatedViews')
    check(ok(current) and len(current['views']) == len(views), 'atomic publication remains complete')
focus = t.view('GetFocusedView')
# A Common Emulator system warning can own keyboard focus. This geometry gate
# verifies consistency, while verify_target_views requires actual positive focus.
published_focus = [v for v in t.view('GetAnnotatedViews')['views'] if v['IsFocused']]
check((ok(focus) and focus['view']['IsFocused'] and any(v['Id'] == focus['view']['Id'] for v in published_focus))
      or (not ok(focus) and not published_focus and bool(focus['return']['Reason'])), 'focus lookup agrees with current publication')
check(not ok(t.view('FindById', {'id': 'missing'})), 'missing view rejected')
forged = copy.deepcopy(photo)
forged['Annotation']['EntityId'] = 'forged'
check(not ok(t.view('ToPresentation', forged)), 'forged identity rejected')
a.report.parent.mkdir(parents=True, exist_ok=True)
a.report.write_text(json.dumps({'serial': a.serial, 'resolution': [a.width, a.height], 'scale': scale,
    'checks': checks, 'views': views, 'focusedView': focus['view']}, indent=2))
print('PASS:', len(checks), 'native geometry/View checks')
