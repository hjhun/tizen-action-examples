#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/DisplayPresentation.sln"
ACTION_BINDINGS=(
    "Tizen.Action.Display|DisplayActions|src/DisplayPresentation.ActionProvider/Generated/DisplayActions.cs"
    "Tizen.Action.View|ViewActions|src/DisplayPresentation.ViewActionProvider/Generated/ViewActions.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
run_app_build "$@"
