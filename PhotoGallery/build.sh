#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TARGET="${APP_DIR}/PhotoGallery.sln"
ACTION_BINDINGS=(
    "Tizen.Action.Photo|PhotoGalleryActionProvider|src/PhotoGallery.ActionProvider/Generated/PhotoGalleryActionProvider.cs"
)

# shellcheck source=../scripts/app-build-common.sh
source "${APP_DIR}/../scripts/app-build-common.sh"
run_app_build "$@"
