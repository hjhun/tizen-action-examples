#!/usr/bin/env bash
# Local Common Emulator packaging only. The caller supplies an existing app directory.
set -euo pipefail
[[ $# -eq 1 ]] || { echo 'Usage: package-dotnet-app.sh <app-directory>' >&2; exit 2; }
PACKAGE_APP_DIR="$(cd "$1" && pwd)"
PACKAGE_APP_NAME="$(basename "$PACKAGE_APP_DIR")"
PACKAGE_PROJECT="${PACKAGE_APP_DIR}/src/${PACKAGE_APP_NAME}.App"
PACKAGE_CONFIGURATION="${CONFIGURATION:-Release}"
tizen build-cs -C "$PACKAGE_CONFIGURATION" -- "$PACKAGE_PROJECT"
PACKAGE_STAGE="$(mktemp -d /tmp/action-package-stage.XXXXXX)"
PACKAGE_OUTPUT="$(mktemp -d /tmp/action-package-output.XXXXXX)"
trap 'rm -rf "$PACKAGE_STAGE" "$PACKAGE_OUTPUT"' EXIT
python3 - "$PACKAGE_PROJECT" "$PACKAGE_CONFIGURATION" "$PACKAGE_STAGE" <<'PY'
from pathlib import Path
import shutil
import sys
project, configuration, stage = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
output = project / 'bin' / configuration / 'net8.0'
for file in output.iterdir():
    if file.is_file(): shutil.copy2(file, stage / file.name)
if (output / 'res').exists(): shutil.copytree(output / 'res', stage / 'res')
else: (stage / 'res').mkdir()
info = stage / 'build.info'
info.write_text('\n'.join('project-path=' + str(stage) if line.startswith('project-path=') else line
                         for line in info.read_text().splitlines()) + '\n')
shutil.copy2(project / 'tizen-manifest.xml', stage / 'tizen-manifest.xml')
PY
# Omitting -s explicitly selects the Tizen Studio emulator-only signer.
tizen package -t tpk -e "$PACKAGE_STAGE" -o "$PACKAGE_OUTPUT" -- "$PACKAGE_STAGE"
python3 - "$PACKAGE_OUTPUT" "$PACKAGE_APP_DIR" "$PACKAGE_PROJECT" <<'PY'
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile, is_zipfile
output, app, project = map(Path, sys.argv[1:])
packages = [p for p in output.iterdir() if p.is_file() and is_zipfile(p)]
assert len(packages) == 1, packages
manifest = (project / 'tizen-manifest.xml').read_bytes()
root = ET.fromstring(manifest)
ui = root.find('{http://tizen.org/ns/packages}ui-application')
with ZipFile(packages[0]) as package:
    assert package.testzip() is None
    assert package.read('tizen-manifest.xml') == manifest
    assert {'author-signature.xml', 'signature1.xml'} <= set(package.namelist())
    assert 'bin/' + ui.attrib['exec'] in package.namelist()
target = app / 'dist' / f"{root.attrib['package']}-{root.attrib['version']}-api{ui.attrib['api-version']}.tpk"
target.parent.mkdir(exist_ok=True)
shutil.copy2(packages[0], target)
print(target)
PY
