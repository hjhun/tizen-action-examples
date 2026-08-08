# Aurum Operations Reference

Read this reference when selecting between accessibility-tree, remote-key, coordinate, and screenshot paths.

## Capability Matrix

| Capability | RPC/command | Evidence |
|---|---|---|
| Transport health | `getScreenSize` / `health` | Status OK and non-zero target dimensions |
| Accessibility tree | `dumpObjectTree` / `tree` | Root count and serialized elements |
| TV-style input | `sendKey` / `key` | Captured focus or screen transition |
| Semantic coordinate click | `click` / `click` | Captured control focus/activation |
| Explicit pointer sequence | `mouseDown` + `mouseUp` / `tap` | Captured pointer postcondition |
| Native screenshot | `takeScreenshot` / `screenshot` | Valid PNG/JPEG with target dimensions |

A capability can work while another is unavailable. In particular, an empty tree does not prevent remote keys, coordinates, or screenshots from working.

## Remote Key Mapping

The bundled client maps these names:

```text
enter → XF86 Return
up    → XF86 Up
down  → XF86 Down
left  → XF86 Left
right → XF86 Right
back  → BACK
home  → HOME
menu  → MENU
```

Unknown key names are sent as raw XF86 key-code strings.

## Coordinate Selection

1. Run `health` to get native width and height.
2. Capture the current frame.
3. Read the control center in target-native coordinates.
4. Send one click or tap.
5. Capture again and verify.

Do not transform coordinates through the Emulator host window. Host decorations and scaling are irrelevant to Aurum coordinates.

## NUI Activation Behavior

NUI controls can expose different pointer and focus semantics:

- A tab or command button may activate with `click`.
- An event card may accept pointer focus but require `Enter` for semantic activation.
- A custom touch binder may require explicit down/up, making `tap` more appropriate.
- A control can change visual focus without changing the application surface.

Use the smallest observed sequence that is supported by a screenshot postcondition. Avoid automatically issuing double-clicks.

## Screenshot Payloads

Observed Aurum services can return either:

- Encoded PNG/JPEG bytes
- A raw BGRA frame of `width × height × 4` bytes

The bundled client queries `getScreenSize`, checks payload length, converts BGRA to RGB, and chooses PNG or JPEG from the output extension. A payload that matches neither shape is rejected instead of being mislabeled.

The default gRPC receive limit is too small for a 1920x1080 raw frame. The bundled client raises the receive limit to 32 MiB.

## Empty Tree Triage

When `tree` returns `status: 0` and `root_count: 0`:

1. Confirm the intended app is foreground/running.
2. Capture a screenshot to prove the active surface.
3. Retry the tree once after a stable layout delay.
4. If still empty, record the tree limitation.
5. Continue with remote keys or calibrated coordinates.

Do not repeatedly restart the app or bootstrap when health and screenshot RPCs already work; that can destroy the state being documented.

## SDB Forwarding

Default path:

```text
host 127.0.0.1:50051
  → sdb forward
  → target localhost:50051
  → org.tizen.aurum-bootstrap
```

Use a different local port when several targets or sessions are active. The remote Aurum port remains 50051 unless the target package is configured otherwise.

## Provenance Template

Use this block in generated documentation:

```text
Target serial: <serial>
Target label: <label if known>
Profile: <common/tv/etc.>
Application ID: <appid>
Resolution: <width>x<height>
Capture path: Aurum takeScreenshot(getPixels=true)
Navigation path: Aurum remote keys and/or coordinate input
Tree capability: <usable / empty roots / unavailable>
Fixture data: <description or none>
Capture date: <date>
```

## Failure Handling

- **Bootstrap missing:** stop; install/enable the approved package rather than fabricating a replacement.
- **Health timeout:** verify process, target port, and SDB forward; restart only the bootstrap if safe.
- **Tree empty:** use the documented fallback.
- **Click status OK but no transition:** inspect focus; try `Enter` or explicit `tap` based on UI behavior.
- **Screenshot too large:** ensure the bundled client is used; it configures a 32 MiB receive limit.
- **Unexpected screenshot bytes:** preserve the byte count in the error report; do not save corrupted output as PNG.
- **Multiple devices:** pass `--serial`; never guess.
