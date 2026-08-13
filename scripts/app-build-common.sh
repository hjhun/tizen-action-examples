#!/usr/bin/env bash
# Shared implementation for per-app generation and Release build entry points.
# The caller must define APP_DIR, BUILD_TARGET, and ACTION_BINDINGS.

set -euo pipefail

: "${APP_DIR:?APP_DIR must be set by the app build script}"
: "${BUILD_TARGET:?BUILD_TARGET must be set by the app build script}"
: "${ACTION_BINDINGS:?ACTION_BINDINGS must be set by the app build script}"

ROOT_DIR="$(cd "${APP_DIR}/.." && pwd)"
ACTIONC_BIN="${ACTIONC_BIN:-actionc}"
ACTIONC_DATA_DIR="${ACTIONC_DATA_DIR:-${ROOT_DIR}/../appfw/tizen-action/default-actions}"
CONFIGURATION="${CONFIGURATION:-Release}"

usage() {
    cat <<'EOF'
Usage: ./build.sh [build|generate|all]

  build     Build the app's solution or primary application project (default).
  generate  Regenerate every declared Action category with actionc.
  all       Run generate, then build.

Environment:
  ACTIONC_BIN       actionc executable (default: actionc)
  ACTIONC_DATA_DIR  Action/entity catalog directory
  CONFIGURATION     dotnet build configuration (default: Release)

Generation changes tracked generated bindings. Review the resulting diff before
committing. It does not package, install, or deploy an application.
EOF
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || {
        echo "Required command not found: $1" >&2
        exit 127
    }
}

apply_tidlc_compatibility() {
    local generated_file="$1"
    python3 - "$generated_file" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text()
direct = "has = HasPrivilegeLocal(b.Sender, item);"
legacy_commented = "//has = HasPrivilegeLocal(b.Sender, item);"
spaced_commented = "// has = HasPrivilegeLocal(b.Sender, item);"
if text.count(legacy_commented) == 1:
    text = text.replace(legacy_commented, "// has = HasPrivilegeLocal(b.Sender, item);\n" +
                        "                        // Disabled for compatibility with runtimes that omit StubBase.HasPrivilegeLocal.\n" +
                        "                        has = false;", 1)
elif text.count(spaced_commented) == 1 and text.count("has = false;") >= 1:
    pass
elif text.count(direct) == 1:
    start = text.rfind("\n", 0, text.index(direct)) + 1
    indent = text[start:text.index(direct)]
    text = text.replace(direct, "// has = HasPrivilegeLocal(b.Sender, item);\n" +
                        f"{indent}// Disabled for compatibility with runtimes that omit StubBase.HasPrivilegeLocal.\n" +
                        f"{indent}has = false;", 1)
else:
    raise SystemExit(f"{path}: unsupported HasPrivilegeLocal generation shape")
path.write_text(text)
PY
}

generate_bindings() {
    require_command "$ACTIONC_BIN"

    if [[ ! -d "$ACTIONC_DATA_DIR" ]]; then
        echo "Action catalog directory not found: $ACTIONC_DATA_DIR" >&2
        exit 2
    fi

    local binding category output_name relative output_base generated_file target_file
    for binding in "${ACTION_BINDINGS[@]}"; do
        IFS='|' read -r category output_name relative <<<"$binding"
        output_base="${APP_DIR}/$(dirname "$relative")/${output_name}"
        generated_file="${output_base}.cs"
        target_file="${APP_DIR}/${relative}"
        mkdir -p "$(dirname "$target_file")"

        echo "Generating ${category} -> ${relative}"
        "$ACTIONC_BIN" -a "$category" -l C# -d "$ACTIONC_DATA_DIR" -o "$output_base"
        [[ -f "$generated_file" ]] || {
            echo "actionc did not create expected binding: $generated_file" >&2
            exit 3
        }
        apply_tidlc_compatibility "$generated_file"
        if [[ "$generated_file" != "$target_file" ]]; then
            mv "$generated_file" "$target_file"
        fi
    done
}

build_app() {
    require_command dotnet
    echo "Building ${BUILD_TARGET#"${ROOT_DIR}/"} (${CONFIGURATION})"
    dotnet build "$BUILD_TARGET" -c "$CONFIGURATION" --nologo
}

run_app_build() {
    local command="${1:-build}"
    shift || true
    [[ $# -eq 0 ]] || { usage >&2; exit 2; }

    case "$command" in
        build) build_app ;;
        generate) generate_bindings ;;
        all)
            generate_bindings
            build_app
            ;;
        -h|--help|help) usage ;;
        *)
            echo "Unknown command: $command" >&2
            usage >&2
            exit 2
            ;;
    esac
}
