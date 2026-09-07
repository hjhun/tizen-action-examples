#!/usr/bin/env python3
"""Generate the single app-owned resolver without modifying platform categories."""
import argparse
import os
from pathlib import Path
import subprocess
APP = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-root', type=Path, default=APP)
args = parser.parse_args()
output = args.output_root / 'src/Browser.ActionProvider/Generated/BrowserCustomActions'
output.parent.mkdir(parents=True, exist_ok=True)
subprocess.run([os.environ.get('ACTIONC_BIN', 'actionc'), '-d', os.environ.get('ACTIONC_DATA_DIR', str(APP.parent.parent / 'appfw/tizen-action/default-actions')),
    '-l', 'C#', '-o', str(output), '-i', str(APP / 'actions/App_Tizen.Action.BrowserCustom_GetPageByIds.action')], check=True)
if not output.with_suffix('.cs').exists():
    raise SystemExit('actionc produced no custom binding')
