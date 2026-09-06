#!/usr/bin/env python3
"""Generate complete standard categories plus Calendar's versioned custom contract."""
import argparse
import os
from pathlib import Path
import subprocess

APP = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--catalog', type=Path, default=Path(os.environ.get(
    'ACTIONC_DATA_DIR', APP.parent.parent / 'appfw/tizen-action/default-actions')))
parser.add_argument('--output-root', type=Path, default=APP)
args = parser.parse_args()

bindings = [
    ('Calendar', 'Calendar.ActionProvider', 'CalendarActionProvider'),
    ('Reminder', 'Calendar.ScheduleActionProvider', 'ScheduleReminderActionProvider'),
    ('View', 'Calendar.ViewActionProvider', 'CalendarViewActionProvider'),
    ('CalendarCustom', 'Calendar.ActionProvider', 'CalendarCustomActionProvider'),
]
for category, project, name in bindings:
    output = args.output_root / 'src' / project / 'Generated' / name
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [os.environ.get('ACTIONC_BIN', 'actionc'), '-d', str(args.catalog),
               '-l', 'C#', '-o', str(output)]
    if category == 'CalendarCustom':
        # Custom ABI v1 order; append future methods, never reorder these inputs.
        for method in ('GetEventByIds', 'SearchInPeriod'):
            command += ['-i', str(APP / 'actions' / f'App_Tizen.Action.CalendarCustom_{method}.action')]
        command += ['-e', str(APP / 'entities')]
    else:
        command += ['-a', 'Tizen.Action.' + category]
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode or not output.with_suffix('.cs').is_file():
        raise SystemExit(result.stdout + result.stderr + '\nNo binding generated.')
    print(f'{category}: {output.with_suffix(".cs")}')
