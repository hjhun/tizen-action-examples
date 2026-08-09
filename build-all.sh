#!/usr/bin/env bash
# Build tracked Tizen Action example applications in a stable, explicit order.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMMAND="${1:-build}"
shift || true

usage() {
    cat <<'EOF'
Usage: ./build-all.sh [build|generate|all]

  build     Build every tracked application without modifying generated source.
            This is the default and is the safe aggregate validation command.
  generate  Regenerate bindings for every application and apply the documented
            fail-closed tidlc compatibility workaround. Review the diff.
  all       Regenerate bindings, then build every application.

The aggregate currently covers Browser, Calendar, Reminder, DisplayPresentation,
and PhotoGallery. Music and Video are untracked workspaces and are deliberately
excluded until they are adopted as repository projects.
EOF
}

case "$COMMAND" in
    build|generate|all) ;;
    -h|--help|help)
        usage
        exit 0
        ;;
    *)
        echo "Unknown command: $COMMAND" >&2
        usage >&2
        exit 2
        ;;
esac

[[ $# -eq 0 ]] || { usage >&2; exit 2; }

apps=(Browser Calendar Reminder DisplayPresentation PhotoGallery)
for app in "${apps[@]}"; do
    echo "===== ${app}: ${COMMAND} ====="
    "${ROOT_DIR}/${app}/build.sh" "$COMMAND"
done
