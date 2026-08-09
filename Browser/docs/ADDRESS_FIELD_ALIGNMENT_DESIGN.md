# Browser Address Field Alignment Design

Date: 2026-08-09
Status: Implemented and validated — visual companion option A
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
- On an empty/Home launch, quick access remains the initial remote-focus target.
- On a restored Page, the UI navigation-state handler explicitly focuses the
  WebView for the correlated terminal Page intent. Superseding navigation cancels
  that request, while a paused Page retains it until resume. Restoration never
  forces address focus or the OSK.
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
7. Capture a fresh 1920 × 1080 native frame before any input and require:
   no OSK, optically centered URL/placeholder, no
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

## Implemented result

- `BrowserAddressShell` owns the capsule surface and 3 px focus outline.
- The native `TextField` is inset 18 px horizontally and 12 px vertically with
  a bounded 34 px text area.
- Pointer click on the shell forwards editing focus to the native field; modal
  state blocks that request.
- Session hydration suppresses workspace/address focus restoration and does not
  open the OSK. A restored Page follows the selected WebView-focus policy.
- Host executable tests 5/5, solution build, Tizen C# build, emulator-test-only
  package, update-install, launch, and 1920 × 1080 native frames passed.
- Unfocused/restored evidence:
  [`images/native-browser-home-address-v2-1920x1080.png`](images/native-browser-home-address-v2-1920x1080.png)
- Focused editing/OSK evidence:
  [`images/native-browser-address-edit-v2-1920x1080.png`](images/native-browser-address-edit-v2-1920x1080.png)
