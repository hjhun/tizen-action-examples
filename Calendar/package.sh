#!/usr/bin/env bash
# Build and sign a local Common Emulator TPK; this does not install it.
set -euo pipefail
CALENDAR_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Tizen CLI also emits build.info, which its package command requires.
tizen build-cs -C "${CONFIGURATION:-Release}" -- "${CALENDAR_DIR}/src/Calendar.App"
CALENDAR_STAGE="$(mktemp -d /tmp/calendar-stage.XXXXXX)"
CALENDAR_PACKAGE_OUTPUT="$(mktemp -d /tmp/calendar-package.XXXXXX)"
trap 'rm -rf "$CALENDAR_STAGE" "$CALENDAR_PACKAGE_OUTPUT"' EXIT
CALENDAR_OUTPUT="${CALENDAR_DIR}/src/Calendar.App/bin/${CONFIGURATION:-Release}/net8.0"
python3 - "$CALENDAR_OUTPUT" "$CALENDAR_STAGE" "$CALENDAR_DIR" <<'PY'
from pathlib import Path
import shutil
import sys
output, stage, app = map(Path, sys.argv[1:])
for path in output.iterdir():
    if path.is_file(): shutil.copy2(path, stage / path.name)
shutil.copytree(output / 'res', stage / 'res')
# The CLI resolves resources against project-path, not the flat assembly folder.
build_info = stage / 'build.info'
lines = build_info.read_text().splitlines()
build_info.write_text('\n'.join('project-path=' + str(stage) if line.startswith('project-path=') else line for line in lines) + '\n')
shutil.copy2(app / 'src/Calendar.App/tizen-manifest.xml', stage / 'tizen-manifest.xml')
PY
# Omitting -s intentionally selects Tizen Studio's emulator-only signer.
tizen package -t tpk -e "$CALENDAR_STAGE" -o "$CALENDAR_PACKAGE_OUTPUT" -- "$CALENDAR_STAGE"
python3 - "$CALENDAR_PACKAGE_OUTPUT" "$CALENDAR_DIR" <<'PY'
from pathlib import Path
import shutil
import sys
from zipfile import ZipFile, is_zipfile
output, app = map(Path, sys.argv[1:])
packages = [p for p in output.iterdir() if p.is_file() and is_zipfile(p)]
assert len(packages) == 1, packages
with ZipFile(packages[0]) as package:
    assert package.testzip() is None
    names = set(package.namelist())
    assert {'tizen-manifest.xml', 'author-signature.xml', 'signature1.xml'} <= names
    for schema in list((app / 'actions').glob('*.action')) + list((app / 'entities').glob('*.entity')):
        assert package.read('res/' + schema.name) == schema.read_bytes()
target = app / 'dist/org.tizen.calendar-0.1.0-api14.tpk'
target.parent.mkdir(exist_ok=True)
shutil.copy2(packages[0], target)
print(target)
PY
