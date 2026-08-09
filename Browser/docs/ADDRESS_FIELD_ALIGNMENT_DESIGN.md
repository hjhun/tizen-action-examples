# Browser Address Field Alignment Design

Date: 2026-08-09
Status: Approved — visual companion option A
Scope: `Browser.App` address/search capsule only

## Decision

Use a balanced address capsule with an outer visual shell and an inset native
`TextField`. The URL must read as part of the compact Samsung-style browser
chrome rather than as an unstyled platform input placed on top of it.

The selected design preserves the existing address/search command and does not
add decorative search, security, account, or AI controls.

## Component structure

```text
BrowserAddressShell (1540 × 58)
└── BrowserAddress TextField
    ├── 18 px horizontal inset
    ├── explicit vertical offset and bounded text height
    └── transparent background and border
```

The outer shell owns the background, corner radius, border, and focus visual.
The native `TextField` owns text editing, caret, submission, placeholder, and
accessibility semantics. This prevents the native field's default geometry from
placing the URL against the capsule's top and left edges.

## Visual contract

- Shell geometry remains 1540 × 58 at the 1920 × 1080 reference canvas.
- URL and placeholder use the existing 4.0-point calibrated type role.
- Text receives an 18 px horizontal inset.
- The TextField receives an explicit vertical offset and bounded height so the
  rendered glyph line is optically centered in the shell.
- Unfocused shell: `#ECECF1`, no prominent outline.
- Focused shell: white surface, 3 px `#0B76E8` outline, restrained soft blue
  halo when supported without adding a second scale transform.
- URL remains single-line and clipped/ellipsized within the available width.
- Reload alignment and the 84 px header geometry do not change.

## Focus and keyboard contract

- Initial launch and session restoration must not focus the address field or
  open the OSK automatically.
- Home quick access remains the initial remote-focus target.
- Selecting the address field explicitly enters editing and may open the OSK.
- Enter submits through the existing navigation command.
- Leaving the field removes only the focused shell treatment; text and current
  page state remain unchanged.
- D-pad traversal and accessibility naming remain unchanged.

## Implementation boundary

Add explicit address-shell metrics to the portable Browser shell contract so
host tests can validate the intended insets and vertical geometry. Update only
hand-written Browser App source and its host test. Generated Action/View source,
domain schemas, provider APIs, and the established C API are outside scope.

## Verification

1. Add a failing host assertion for shell size, horizontal inset, vertical
   offset, bounded field height, and focus-outline width.
2. Implement the outer shell and inset transparent TextField.
3. Add a failing focus-policy assertion proving session restoration does not
   request address focus; implement the minimal policy change.
4. Run all five Browser executable host tests and the Browser solution build.
5. Build and package with the explicit Common Emulator test-only signing mode.
6. Update-install and launch on the Tizen 10.1 Common Emulator.
7. Capture a fresh 1920 × 1080 native Home frame before any input and require:
   no OSK, visible Home-entry focus, optically centered URL/placeholder, no
   clipping, and unchanged Internet/Reload geometry.
8. Explicitly focus the address field, capture the editing state, and require a
   centered URL, visible caret, bounded focus outline, and successful Enter
   navigation.

## Non-goals

- Changing navigation, tab persistence, Action, Entity, ViewAnnotation, or A2UI
  behavior.
- Adding a security badge or search icon from the unselected option B.
- Introducing display/edit view swapping from the unselected option C.
- Claiming TV-profile approval from Common Emulator evidence.
