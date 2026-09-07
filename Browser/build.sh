#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/Browser.sln"
ACTION_BINDINGS=(
    "Tizen.Action.Browser|TizenActionBrowser|src/Browser.ActionProvider/TizenActionBrowserGenerated.cs"
    "Tizen.Action.View|TizenActionView|src/Browser.ViewActionProvider/TizenActionView.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
if [[ "${1:-build}" == all && $# -eq 1 ]]; then
    run_app_build generate
    python3 "${APP_DIR}/generate-custom-bindings.py"
    run_app_build build
else
    run_app_build "$@"
    if [[ "${1:-build}" == generate ]]; then python3 "${APP_DIR}/generate-custom-bindings.py"; fi
fi
