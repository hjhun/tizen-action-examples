#!/usr/bin/env python3
"""Generate the complete current Presentation and View categories without patching output."""
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
for category, project, name in [
    ('Presentation', 'ActionProvider', 'DisplayActions'),
    ('View', 'ViewActionProvider', 'ViewActions'),
]:
    output = args.output_root / 'src' / ('DisplayPresentation.' + project) / 'Generated' / name
    output.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run([os.environ.get('ACTIONC_BIN', 'actionc'), '-d', str(args.catalog),
        '-a', 'Tizen.Action.' + category, '-l', 'C#', '-o', str(output)], capture_output=True, text=True)
    if result.returncode or not output.with_suffix('.cs').is_file():
        raise SystemExit(result.stdout + result.stderr + '\nNo binding generated.')
    print(f'{category}: {output.with_suffix(".cs")}')
