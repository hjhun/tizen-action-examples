#!/usr/bin/env bash
set -euo pipefail
GALLERY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$GALLERY_DIR"
for project in tests/*.Tests; do dotnet run --project "$project"; done
