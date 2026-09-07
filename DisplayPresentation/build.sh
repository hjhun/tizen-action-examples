#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[[ $# -le 1 ]] || { echo 'Usage: ./build.sh [build|generate|all]' >&2; exit 2; }
case "${1:-build}" in
    generate|all) python3 "${APP_DIR}/generate-bindings.py" ;;
    build) ;;
    -h|--help) echo 'Usage: ./build.sh [build|generate|all]'; exit 0 ;;
    *) echo 'Usage: ./build.sh [build|generate|all]' >&2; exit 2 ;;
esac
if [[ "${1:-build}" != generate ]]; then
    dotnet build "${APP_DIR}/DisplayPresentation.sln" -c "${CONFIGURATION:-Release}" --nologo
fi
