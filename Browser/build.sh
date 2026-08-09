#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/Browser.sln"
ACTION_BINDINGS=(
    "Tizen.Action.Browser|TizenActionBrowser|src/Browser.ActionProvider/TizenActionBrowserGenerated.cs"
    "Tizen.Internal.Action.View|TizenInternalActionView|src/Browser.ViewActionProvider/TizenInternalActionViewGenerated.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
run_app_build "$@"
