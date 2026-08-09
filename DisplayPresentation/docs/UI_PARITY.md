# DisplayPresentation UI parity ledger

Profile: [A2UI Samsung One UI Profile v0.1](A2UI_ONE_UI_PROFILE.md)
Canonical executable preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)

## Mapping and evidence rule

The preview parses the bounded local Presentation fixture before rendering `Column`/`Text` semantics. Its `OneUiSection` and `OneUiText` map to the planned reusable NUI components; its profile-owned recovery button maps to `OneUiButton`. The verification-state switcher is outside the product canvas and is not an NUI product control.

| Profile component/state | HTML state | Planned NUI mapping | HTML capture | Native Aurum capture | Comparison / status |
|---|---|---|---|---|---|
| `Column` + headline/supporting/body `Text` | Valid | `OneUiSection` + `OneUiText` | Pending browser capture | Pending | NUI composition and generated-entity View snapshot mapping host-build; installed comparison pending. |
| Parser loading | Loading | profile-owned loading surface | Pending browser capture | Pending | NUI loading composition remains pending. |
| malformed/mismatched/oversized input | Malformed | profile-owned safe error surface + `Dismiss` | Pending browser capture | Pending | NUI recovery control is host-built; installed comparison pending. |
| unsupported component/property | Unsupported | profile-owned unsupported recovery surface + `Dismiss` | Pending browser capture | Pending | NUI recovery control is host-built; installed comparison pending. |
| disabled command | Disabled control | `OneUiButton` | Pending browser capture | Pending | Button mapping is future profile work. |
| focused recovery | Malformed then D-pad/keyboard | `OneUiButton` measured-focus View | Pending browser capture | Pending | Focus semantics defined, not native-verified. |

## Cross-app Presentation flow ledger

| Source flow | Current source finding | HTML fixture/capture | DisplayPresentation native capture | Result |
|---|---|---|---|---|
| Browser `Tizen.Action.Browser_ToPresentation` | Current producer emits `surfaceUpdate` with an empty `components` array and a document shape without the v0.1 required matching `surfaceId`/`value`; it is not a valid positive profile fixture. | Blocked by source fixture contract | Pending | Blocked; Browser-owned producer must emit current bounded A2UI. |
| PhotoGallery Presentation / `View_ToPresentation` | No producer or Presentation output was found in current PhotoGallery C# source. | Blocked by missing source fixture | Pending | Blocked; PhotoGallery-owned producer must exist before integration evidence. |

## Required parity closure

For each row, capture the HTML frame after browser verification and then the same installed Common Emulator state through Aurum. Compare hierarchy, geometry, type scale, spacing, colors, component state, focus, labels, density, and reference-canvas scaling. Store only validated native images under `docs/images/`; link both capture paths here. No native screenshot, target install, or Telegram media has been produced by this slice.
