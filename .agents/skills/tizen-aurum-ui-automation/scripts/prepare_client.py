#!/usr/bin/env python3
"""Prepare a cached Python gRPC client for the bundled Aurum protocol."""

from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys

SKILL_ROOT = Path(__file__).resolve().parents[1]
PROTO = SKILL_ROOT / "references" / "aurum.proto"
DEFAULT_CACHE = Path(os.environ.get("TIZEN_AURUM_CACHE", "~/.cache/tizen-aurum-ui-automation")).expanduser()


def run(command: list[str]) -> None:
    print("+", " ".join(command))
    subprocess.run(command, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache-dir", type=Path, default=DEFAULT_CACHE)
    parser.add_argument("--force", action="store_true", help="recreate the cached virtual environment")
    args = parser.parse_args()

    cache = args.cache_dir.expanduser().resolve()
    venv = cache / "venv"
    generated = cache / "generated"
    stamp = cache / "READY"
    token = hashlib.sha256(PROTO.read_bytes()).hexdigest()
    required = [generated / "aurum_pb2.py", generated / "aurum_pb2_grpc.py"]

    if (
        not args.force
        and stamp.is_file()
        and stamp.read_text(encoding="utf-8").strip() == token
        and (venv / "bin" / "python").is_file()
        and all(path.is_file() for path in required)
    ):
        print(f"Aurum client cache is already ready: {cache}")
        return 0

    if args.force and venv.exists():
        shutil.rmtree(venv)
    cache.mkdir(parents=True, exist_ok=True)
    generated.mkdir(parents=True, exist_ok=True)

    if not venv.exists():
        run([sys.executable, "-m", "venv", str(venv)])

    python = venv / "bin" / "python"
    run([
        str(python), "-m", "pip", "install", "--disable-pip-version-check",
        "grpcio", "grpcio-tools", "protobuf", "Pillow",
    ])
    run([
        str(python), "-m", "grpc_tools.protoc",
        "-I", str(PROTO.parent),
        f"--python_out={generated}",
        f"--grpc_python_out={generated}",
        str(PROTO),
    ])

    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError(f"Aurum stub generation failed: {missing}")

    stamp.write_text(token + "\n", encoding="utf-8")
    print(f"Aurum client cache is ready: {cache}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
