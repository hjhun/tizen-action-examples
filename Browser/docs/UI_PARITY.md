# Browser UI parity ledger

- Korean parity guide: pending after native evidence exists. This technical ledger is intentionally English until its bilingual documentation counterpart is created with the implemented product.
- Canonical preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)
- Reference/adaptation record: [`ONE_UI_REFERENCE.md`](ONE_UI_REFERENCE.md)
- Status: **HTML reference baseline browser-verified; installed NUI parity is blocked and must not be inferred.**
- Validated HTML captures: [`images/html-browser-command-band-1280x720.png`](images/html-browser-command-band-1280x720.png), [`images/html-browser-home-1280x720.png`](images/html-browser-home-1280x720.png), and [`images/html-browser-offline-1264x625.png`](images/html-browser-offline-1264x625.png). These are preview evidence only, never native/Aurum evidence.
- Native Common Emulator captures: [`images/native-browser-command-band-1920x1080.png`](images/native-browser-command-band-1920x1080.png) and [`images/native-browser-address-focus-1920x1080.png`](images/native-browser-address-focus-1920x1080.png). Aurum decoded both as 1920×1080 RGB PNGs; the post-Right-key image differs only in the command band's address-field rectangle. They prove launch/capture/input response, not a completed visual comparison.

## Current mapping

| Sample surface/control/state | Planned NUI/runtime mapping | Command or contract | Annotation / A2UI | Native parity evidence |
|---|---|---|---|---|
| Full 1920×1080 canvas scaled in its host frame | Full-window physical root plus one centered uniform NUI reference canvas, with the real `WebView` mounted only in the content rectangle | App render policy; `Window.Default.WindowSize` and `GetInsets()` calculate the safe viewport and retain the prior frame during transient invalid geometry | Bounds must be measured after final transform | Aurum capture retained at 1920×1080; non-zero-inset geometry comparison remains open |
| Header: Back, Forward, Reload, address/search, Tabs | Persistent NUI command band with discrete Back/Forward disabled controls, Reload, `TextField`, and Tabs control above `WebView` | Reload and submitted absolute URL use the shared navigation coordinator; Back/Forward/Tabs remain local follow-up commands | Chrome itself is not currently published; selected page is the planned annotated surface | Aurum Right-key postcondition changed only the address-field rectangle; control-level semantic/focus comparison remains open |
| Current title/URL context and WebView content region | `TextLabel` page context plus a real system `WebView` content rectangle | `BrowserNavigationCoordinator` / `IWebRuntime` completion updates chrome state | `Tizen.Entity.Browser` uses generated `ToJson()` in `Annotation.EntityInfo` | Host compiled; native layout/annotation still unverified |
| Loading band | NUI progress view driven by `PageLoadStarted` / completion/error | `NuiWebViewRuntime` async outcome | Current A2UI must include page/load state derived from same entity/render state | Not implemented visually; no capture |
| Offline and engine-error pages; Retry/Back | NUI recovery overlay or content state with focus trap/restoration | WebView failed/timeout outcome; retry uses same navigation reducer | Error state belongs in current A2UI Template/Document | Not implemented; no capture |
| Tabs manager: select, new tab, close | NUI tab list with discrete remote-focus rows and close controls | Planned local tab commands; no extra public Action justified | Selected normal-mode public page only; private data excluded | Not implemented; no capture |
| Close-tab dialog with Cancel/Close and Back cancellation | NUI modal overlay, modal focus trap, restored invoking tab focus | Planned local close confirmation command | No stale annotations while modal hides/replaces an annotated page | Not implemented; no capture |
| Keyboard arrows/Enter/Escape and pointer/touch | NUI focus graph, remote key handling, pointer down/up-inside parity | Same reducer/commands for remote, keyboard, pointer, touch | Focus snapshot must come from actual NUI `FocusManager` | Not verified on target |
| Browser `ToPresentation` / `View_ToPresentation` | Current-state A2UI producer + Samsung One UI `DisplayPresentation` renderer | Existing `Tv_Tizen.Action.Browser_ToPresentation`; `Tizen.Internal.Action.View_ToPresentation` | Separate valid `surfaceUpdate` Template and `dataModelUpdate` Document from the same generated Browser entity and rendered state | Source compiles, but target RPC is blocked by generated `HasPrivilegeLocal` mismatch; renderer round trips not run |

## Required evidence loop

For each row before it can be marked **matched**:

1. Capture the equivalent sample state from the canonical HTML preview and retain it under `Browser/docs/images/` only after validating dimensions and content.
2. Implement the mapped NUI slice; build, package, install, and reach the same state using actual Aurum remote/pointer input.
3. Capture and validate the native screenshot under `Browser/docs/images/`.
4. Compare hierarchy, geometry, typography, spacing, color, controls, content density, state, focus, and scaling. Record both image paths and either close the difference or state an intentional target adaptation.
5. For Entity/Presentation rows, separately prove Action → Presentation → DisplayPresentation and ViewAnnotation → View_ToPresentation → DisplayPresentation with current state, not canned data.

## Known difference and blocker

The sample command band was corrected from an implicit four-item CSS grid to the intended one-row Browser hierarchy: 190-unit brand region, 218-unit navigation cluster, flexible address field, and 150-unit Tabs control. Its current capture is retained above. The matching NUI root now calculates its centered reference canvas inside actual window insets and preserves the last valid frame on transient invalid geometry. The selected emulator's retained native frames still prove only the zero-inset command-band launch/focus response; non-zero-inset geometry and full visual review remain open.

The source now has the first persistent command-band/current-context slice over a real content-only system `WebView`, but it still lacks the sample's loading band, recovery overlay, tabs manager, confirmation flow, and verified native focus behavior. This is an open product difference, not an acceptable parity match.

Additionally, the selected Common Emulator dispatches the generated provider until `StubBase.HasPrivilegeLocal` then terminates with `MissingMethodException`. Provider discovery is proven, but typed Action/View RPC, current A2UI payload validation, DisplayPresentation round trips, and Aurum Browser screenshots remain blocked. See the Goal `BLOCKERS.md` for the reproducible condition.
