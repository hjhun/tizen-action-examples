#!/usr/bin/env bash
set -euo pipefail
APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$APP_DIR/../scripts/package-dotnet-app.sh" "$APP_DIR"
