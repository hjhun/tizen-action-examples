#!/usr/bin/env python3
"""Generate the complete Photo, View and PhotoGalleryCustom v1 bindings."""
import argparse
import os
from pathlib import Path
import subprocess

APP = Path(__file__).resolve().parent
CUSTOM_METHODS = ('GetPhotoByIds', 'SetFavorite')
# Unregistered categories use the runtime's alphabetical fallback. These v1 slots
# are fixed; future additions must preserve them or use a new versioned category.
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--catalog', type=Path, default=Path(os.environ.get(
    'ACTIONC_DATA_DIR', APP.parent.parent / 'appfw/tizen-action/default-actions')))
parser.add_argument('--output-root', type=Path, default=APP)
args = parser.parse_args()
for category, project, name in (
    ('Photo', 'PhotoGallery.ActionProvider', 'PhotoGalleryActionProvider'),
    ('PhotoGalleryCustom', 'PhotoGallery.ActionProvider', 'PhotoGalleryCustomActionProvider'),
    ('View', 'PhotoGallery.ViewActionProvider', 'PhotoGalleryViewActionProvider'),
):
    output = args.output_root / 'src' / project / 'Generated' / name
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [os.environ.get('ACTIONC_BIN', 'actionc'), '-d', str(args.catalog), '-l', 'C#', '-o', str(output)]
    if category == 'PhotoGalleryCustom':
        for method in CUSTOM_METHODS:
            command += ['-i', str(APP / 'actions' / f'App_Tizen.Action.PhotoGalleryCustom_{method}.action')]
    else:
        command += ['-a', 'Tizen.Action.' + category]
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode or not output.with_suffix('.cs').is_file():
        raise SystemExit(result.stdout + result.stderr + '\nNo binding generated.')
    print(f'{category}: {output.with_suffix(".cs")}')
