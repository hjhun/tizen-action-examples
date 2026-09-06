#!/usr/bin/env bash
set -euo pipefail
GALLERY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$GALLERY_DIR/../scripts/package-dotnet-app.sh" "$GALLERY_DIR"
