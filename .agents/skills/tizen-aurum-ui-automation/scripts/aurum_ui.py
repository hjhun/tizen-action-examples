#!/usr/bin/env python3
"""Control and capture a Tizen UI through the Aurum bootstrap gRPC service."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from typing import Any

try:
    import grpc  # type: ignore[import-not-found]
    import aurum_pb2 as pb  # type: ignore[import-not-found]
    import aurum_pb2_grpc as pb_grpc  # type: ignore[import-not-found]
except ImportError as error:
    raise SystemExit(
        "Aurum client dependencies are not ready. Run scripts/prepare_client.py, "
        "then invoke this file through scripts/aurum-ui."
    ) from error

BOOTSTRAP_APP_ID = "org.tizen.aurum-bootstrap"
DEFAULT_HOST = os.environ.get("TIZEN_AURUM_HOST", "127.0.0.1")
DEFAULT_PORT = int(os.environ.get("TIZEN_AURUM_PORT", "50051"))
MAX_MESSAGE_BYTES = 32 * 1024 * 1024


def run(command: list[str], *, capture: bool = False, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, check=check, text=True, capture_output=capture)


def detect_serial(explicit: str | None) -> str:
    if explicit:
        return explicit
    env_serial = os.environ.get("TIZEN_SERIAL")
    if env_serial:
        return env_serial
    result = run(["sdb", "devices"], capture=True)
    devices: list[str] = []
    for line in result.stdout.splitlines():
        columns = line.split()
        if len(columns) >= 2 and columns[1] == "device":
            devices.append(columns[0])
    if len(devices) != 1:
        raise SystemExit(f"Expected exactly one connected Tizen target, found {devices}; pass --serial.")
    return devices[0]


def sdb(serial: str, *args: str, capture: bool = False, check: bool = True) -> subprocess.CompletedProcess[str]:
    return run(["sdb", "-s", serial, *args], capture=capture, check=check)


def channel(host: str, port: int):
    return grpc.insecure_channel(
        f"{host}:{port}",
        options=[("grpc.max_receive_message_length", MAX_MESSAGE_BYTES)],
    )


def stub(host: str, port: int):
    return pb_grpc.BootstrapStub(channel(host, port))


def health(client, timeout: float = 5.0) -> dict[str, Any]:
    response = client.getScreenSize(pb.ReqGetScreenSize(), timeout=timeout)
    if response.status != pb.OK:
        raise RuntimeError(f"Aurum screen-size health probe failed with status {response.status}")
    return {
        "status": "ok",
        "width": response.size.width,
        "height": response.size.height,
    }


def start_session(args: argparse.Namespace) -> dict[str, Any]:
    serial = detect_serial(args.serial)
    package = sdb(
        serial, "shell", f"pkginfo --pkg {BOOTSTRAP_APP_ID}", capture=True, check=False
    )
    if package.returncode != 0 or BOOTSTRAP_APP_ID not in package.stdout:
        raise SystemExit(f"{BOOTSTRAP_APP_ID} is not installed on {serial}.")

    sdb(serial, "shell", f"app_launcher -s {BOOTSTRAP_APP_ID}", check=False)
    sdb(serial, "forward", "--remove", f"tcp:{args.port}", capture=True, check=False)
    sdb(serial, "forward", f"tcp:{args.port}", f"tcp:{args.remote_port}")

    client = stub(args.host, args.port)
    last_error: Exception | None = None
    for _ in range(args.retries):
        try:
            info = health(client, timeout=3.0)
            return {"serial": serial, "forward": f"{args.port}->{args.remote_port}", **info}
        except Exception as error:  # gRPC exposes several transport exception classes.
            last_error = error
            time.sleep(args.retry_delay)
    raise RuntimeError(f"Aurum did not become ready: {last_error}")


def stop_session(args: argparse.Namespace) -> dict[str, Any]:
    serial = detect_serial(args.serial)
    sdb(serial, "forward", "--remove", f"tcp:{args.port}", capture=True, check=False)
    if args.stop_bootstrap:
        sdb(serial, "shell", f"app_launcher -t {BOOTSTRAP_APP_ID}", check=False)
    return {"serial": serial, "forward_removed": args.port, "bootstrap_stopped": args.stop_bootstrap}


def element_to_dict(element, depth: int, max_depth: int) -> dict[str, Any]:
    result = {
        "elementId": element.elementId,
        "text": element.text,
        "widgetType": element.widgetType,
        "role": element.role,
        "automationId": element.automationId,
        "description": element.description,
        "geometry": {
            "x": element.geometry.x,
            "y": element.geometry.y,
            "width": element.geometry.width,
            "height": element.geometry.height,
        },
        "isClickable": element.isClickable,
        "isEnabled": element.isEnabled,
        "isFocused": element.isFocused,
        "isFocusable": element.isFocusable,
        "isShowing": element.isShowing,
        "isVisible": element.isVisible,
    }
    if max_depth < 0 or depth < max_depth:
        result["children"] = [element_to_dict(child, depth + 1, max_depth) for child in element.child]
    return result


def save_screenshot(client, output: Path) -> dict[str, Any]:
    size = health(client)
    payload = b"".join(
        chunk.image
        for chunk in client.takeScreenshot(pb.ReqTakeScreenshot(getPixels=True), timeout=20)
    )
    output = output.expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    if payload.startswith(b"\x89PNG") or payload.startswith(b"\xff\xd8"):
        output.write_bytes(payload)
    else:
        expected = size["width"] * size["height"] * 4
        if len(payload) != expected:
            raise RuntimeError(
                f"Unexpected screenshot payload: {len(payload)} bytes; expected encoded image or {expected} BGRA bytes"
            )
        from PIL import Image

        image = Image.frombytes(
            "RGBA", (size["width"], size["height"]), payload, "raw", "BGRA"
        ).convert("RGB")
        suffix = output.suffix.lower()
        if suffix in {".jpg", ".jpeg"}:
            image.save(output, "JPEG", quality=92)
        else:
            image.save(output, "PNG")

    return {
        "output": str(output),
        "width": size["width"],
        "height": size["height"],
        "source_bytes": len(payload),
        "saved_bytes": output.stat().st_size,
    }


def add_connection_options(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    start = sub.add_parser("session-start", help="launch Aurum bootstrap and create an SDB port forward")
    start.add_argument("--serial")
    start.add_argument("--host", default=DEFAULT_HOST)
    start.add_argument("--port", type=int, default=DEFAULT_PORT)
    start.add_argument("--remote-port", type=int, default=50051)
    start.add_argument("--retries", type=int, default=6)
    start.add_argument("--retry-delay", type=float, default=1.0)

    stop = sub.add_parser("session-stop", help="remove the SDB forward")
    stop.add_argument("--serial")
    stop.add_argument("--port", type=int, default=DEFAULT_PORT)
    stop.add_argument("--stop-bootstrap", action="store_true")

    probe = sub.add_parser("health", help="probe Aurum and report screen size")
    add_connection_options(probe)

    tree = sub.add_parser("tree", help="dump the Aurum accessibility tree")
    add_connection_options(tree)
    tree.add_argument("--element-id", default="")
    tree.add_argument("--max-depth", type=int, default=-1)

    key = sub.add_parser("key", help="send a remote key")
    add_connection_options(key)
    key.add_argument("name", help="back, home, menu, enter, up, down, left, right, or raw key code")
    key.add_argument("--count", type=int, default=1)
    key.add_argument("--delay", type=float, default=0.35)

    for name, help_text in [
        ("click", "invoke Aurum coordinate click"),
        ("tap", "send mouse-down and mouse-up at a coordinate"),
        ("move", "move the Aurum pointer"),
    ]:
        command = sub.add_parser(name, help=help_text)
        add_connection_options(command)
        command.add_argument("x", type=int)
        command.add_argument("y", type=int)

    shot = sub.add_parser("screenshot", help="capture a native PNG or JPEG")
    add_connection_options(shot)
    shot.add_argument("output", type=Path)
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.command == "session-start":
        result = start_session(args)
    elif args.command == "session-stop":
        result = stop_session(args)
    else:
        client = stub(args.host, args.port)
        if args.command == "health":
            result = health(client)
        elif args.command == "tree":
            response = client.dumpObjectTree(
                pb.ReqDumpObjectTree(elementId=args.element_id), timeout=15
            )
            result = {
                "status": int(response.status),
                "root_count": len(response.roots),
                "roots": [element_to_dict(root, 0, args.max_depth) for root in response.roots],
            }
        elif args.command == "key":
            direct = {"back": pb.ReqKey.BACK, "home": pb.ReqKey.HOME, "menu": pb.ReqKey.MENU}
            xf86 = {"enter": "Return", "up": "Up", "down": "Down", "left": "Left", "right": "Right"}
            statuses: list[int] = []
            for _ in range(args.count):
                request = pb.ReqKey(actionType=pb.ReqKey.STROKE)
                if args.name in direct:
                    request.type = direct[args.name]
                else:
                    request.type = pb.ReqKey.XF86
                    request.XF86keyCode = xf86.get(args.name, args.name)
                response = client.sendKey(request, timeout=5)
                statuses.append(int(response.status))
                if response.status != pb.OK:
                    raise RuntimeError(f"Aurum key failed with status {response.status}")
                time.sleep(args.delay)
            result = {"key": args.name, "count": args.count, "statuses": statuses}
        elif args.command == "click":
            response = client.click(
                pb.ReqClick(type=pb.ReqClick.COORD, coordination=pb.Point(x=args.x, y=args.y)),
                timeout=5,
            )
            result = {"click": [args.x, args.y], "status": int(response.status)}
        elif args.command == "tap":
            point = pb.Point(x=args.x, y=args.y)
            down = client.mouseDown(pb.ReqMouseDown(button=1, coordination=point), timeout=5)
            time.sleep(0.12)
            up = client.mouseUp(pb.ReqMouseUp(button=1, coordination=point), timeout=5)
            result = {"tap": [args.x, args.y], "statuses": [int(down.status), int(up.status)]}
        elif args.command == "move":
            response = client.mouseMove(
                pb.ReqMouseMove(button=1, coordination=pb.Point(x=args.x, y=args.y)), timeout=5
            )
            result = {"move": [args.x, args.y], "status": int(response.status)}
        else:
            result = save_screenshot(client, args.output)

    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
