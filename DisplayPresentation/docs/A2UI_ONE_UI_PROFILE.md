# Google A2UI Compatibility and Samsung One UI Rendering Profile v0.1

> 이 문서는 Google A2UI protocol compatibility와 Samsung One UI-adapted Tizen NUI rendering을 분리한다. Presentation payload는 공식 A2UI semantics와 data를 제공하며 visual skin이나 executable content를 제공하지 않는다.

## Sources, versions, and adaptation

- **Samsung Developer, “One UI”** (retrieved 2026-08-09): One UI’s large-screen guidance emphasizes a calm, content-first hierarchy, clear grouping, comfortable reach/focus, and adaptable layouts rather than copied Galaxy assets. Source: <https://developer.samsung.com/one-ui>.
- **Canonical A2UI repository**, revision [`ec97cb0d7499932e67003ffe5b709a3db7e7033a`](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a), committed 2026-08-07 and retrieved 2026-08-09: <https://github.com/a2ui-project/a2ui>. The former `google/A2UI` repository redirects here.
- **Version status at that revision:** [v0.9.1](https://github.com/a2ui-project/a2ui/blob/ec97cb0d7499932e67003ffe5b709a3db7e7033a/specification/v0_9_1/docs/a2ui_protocol.md) is **Current Production**; [v1.0](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a/specification/v1_0) is **Candidate**, not stable. v0.9.1 defines `createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`, `catalogId`, and client `action`. v1.0 candidate adds/changes contracts including action IDs/`actionResponse` and `surfaceProperties`.
- **Tizen NUI API10**, public [`Tizen.NET.API10` 10.0.0.17508 package](https://www.nuget.org/packages/Tizen.NET.API10/10.0.0.17508) XML documentation inspected 2026-08-09: [`NUIApplication(string, WindowMode)` / `WindowMode.Transparent`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Application/NUIApplication.cs), [`Window.SetTransparency`, `SetOpaqueState`, `SetInputRegion`, `SetAcceptFocus`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Window/Window.cs), and [`Color.Transparent`](https://github.com/Samsung/TizenFX/blob/3cc2ad9a6409ada349c243232c9d16c0d1d02e60/src/Tizen.NUI/src/public/Common/Color.cs). The former <https://docs.tizen.org/application/dotnet/api/TizenFX/API10/api/Tizen.NUI.html> index currently redirects to the current Tizen docs portal, so the package XML plus source permalinks are the recorded API10 evidence. The API surface is documented; target compositor behavior is not yet verified.

This renderer is an **Inspect / Operate** surface, not a landing page or dashboard. The adaptation uses a full-window neutral canvas, a compact context heading, readable content grouping, conservative rounded containers only when a semantic section needs containment, and a high-visibility focus ring. The protected `Music/refs/music-design.html` exemplar additionally validates the reusable TV translation method: centered 1920×1080 reference-canvas scaling, TV-distance type/spacing, strong content-first hierarchy, compact contextual chrome, persistent task context where justified, two-cue focus, and deterministic multi-input transitions. This profile does not copy its Music name/logo, rose/font/media tokens, playback/library controls, domain UI, gradients, or exact geometry, and does not copy Samsung proprietary assets.

Google A2UI가 versioned wire contract, surface lifecycle, catalog, semantic component, data binding과 client action을 소유한다. Samsung One UI adaptation은 그 의미를 Tizen NUI component, typography, spacing, shape, color, focus와 input으로 표현하는 renderer 책임이다. Renderer support matrix는 Google A2UI catalog를 재정의하지 않는다.

## Wire envelope and safety boundary

The current Tizen Presentation compatibility adapter requires `Template` to be one JSON object with legacy v0.8 `surfaceUpdate` and `Document` to be one JSON object with matching `dataModelUpdate`. Both are untrusted. This split pair is retained for existing producers but is **not** labeled v0.9.1. A future canonical adapter must explicitly negotiate version/catalog and process the matching v0.9.1 lifecycle (`createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`) or a separately declared candidate profile; message names from different versions may not be mixed. Every adapter performs JSON parsing, type checks, schema/profile validation, binding resolution, depth/count/string limits, lifecycle/order checks, and stale-request checks before the NUI renderer receives a semantic tree.

```text
Presentation strings
  → bounded parser / profile validator
  → immutable SemanticSurface (not JSON)
  → OneUiProfileMapper + tokens
  → reusable NUI actors + one input reducer
  → measured visible View snapshot / View_ToPresentation
```

Payload values may not supply HTML, script, URL, image, font, color, spacing, dimensions, transforms, event handlers, or arbitrary layout. Unknown components/properties and stale/mismatched requests return typed failure and render the profile-owned recovery state; they never receive a guessed card fallback.

`catalogId` chooses a mutually supported semantic component/function catalog; it does not choose the native visual skin. DisplayPresentation owns native component selection, tokens, layout, accessibility, focus, and input behavior. Renderer-local functions are allowlisted, and agent events become bounded version-correct `action` messages. A2UI's logical surface lifecycle has no authority over the NUI/OS window.

## Logical surface, buffer, and native window

| Layer | Meaning | What it does not prove |
|---|---|---|
| 32-bit ARGB8888 buffer | Four 8-bit channels available to store each rendered pixel. | That alpha reaches/is blended by the window compositor, that another app is visible, or that input passes through. |
| Per-pixel-alpha native window | A transparent/translucent NUI window whose compositor blends pixel alpha. API10 provides `WindowMode.Transparent`, `SetTransparency(true)`, and alpha-zero `Color.Transparent`; `SetOpaqueState` is a visual-occlusion hint only. | Buffer format, A2UI conformance, pointer hit regions, key focus, runtime target support, or screenshot correctness. |
| A2UI logical surface | Version/catalog-scoped component/data state with create/update/delete lifecycle and actions. | An ARGB8888 buffer, a transparent/32-bit OS window, compositing, z-order, pass-through, or focus policy. |

The default/effective supported host remains opaque until a capability probe and installed Common Emulator evidence pass. Transparent overlay mode must fail closed to that opaque host. Input is independently configured and verified: transparency alone never means click-through; use only documented input regions/focus policy and prove D-pad/key, pointer/touch, Back, focus restoration, and pause/resume behavior.

## v0.1 renderer semantic matrix

The host parser currently implements the bounded legacy-compatible `Column`/path-bound-`Text` semantic-tree portion of this renderer matrix. `Button` and `TextField` remain renderer-specified but unimplemented and therefore reject as typed unsupported input until their official A2UI mapping, reducer, and NUI components are added. This matrix records renderer support; it does not redefine the Google A2UI catalog.

| A2UI component | Accepted v0.1 properties/bindings | Semantic node | reusable NUI component | One UI treatment | Input/state behavior | bounds and privacy |
|---|---|---|---|---|---|---|
| `Column` | `id`; ordered `children` component IDs; no styling props | `VerticalGroup` | `OneUiSection` / `OneUiStack` | 24dp outer gutter, 16dp related-item gap, neutral full-window surface; section container only for explicit grouping | Not independently focusable. D-pad traverses its enabled focusable descendants. | Maximum depth 4, 32 nodes; IDs ≤64 ASCII-safe chars; no hidden content node publication. |
| `Text` | `id`; `text.path` to a scalar string in document `value`; optional profile enum `role`: `headline`, `title`, `body`, `label`, `supporting` | `TextValue` | `OneUiText` | Profile picks type, never payload font: headline 32sp/semibold, title 24sp/semibold, body 18sp/regular, label 14sp/medium, supporting 16sp/regular; ink/muted tokens provide hierarchy | Static, non-focusable unless it is the label child of a supported control. Loading uses profile skeleton; error is profile-owned. | One bound value ≤256 chars; only scalar strings; text is escaped; source data fields not bound into rendered nodes are not exposed. |
| `Button` | `id`; exactly one `Text` label child; `action.name` from a registered allowlist; `enabled` boolean binding | `Command` | `OneUiButton` | Primary filled button or secondary outlined button is selected by registered action semantics, not payload color; minimum 48dp hit/focus height | D-pad/keyboard arrows move in deterministic document order; Enter/Space and pointer Down+Up-inside dispatch the same command; disabled is not focusable; Back restores invoker after profile modal recovery. | Action names ≤64 chars and must be registered by app; no payload callback/script; action argument is bounded schema data only. |
| `TextField` | `id`; `label` Text child; `value.path`; `inputType` enum `shortText`/`number`/`obscured`; `enabled` binding | `Input` | `OneUiTextField` | Label above field, 1dp neutral outline, 12dp radius, 16dp internal padding; focused outline + elevation/scale cue | Enter begins/commits edit according to active editor; pointer focuses; Back cancels edit and restores focus; obscured text is never reflected in Annotation or Presentation snapshot. | Maximum input 256 chars; no remote validation or arbitrary regex; obscured values are redacted. |

All other catalogue components/properties are **unsupported in v0.1**. The typed result is `Unsupported` with the component/property name and the visible profile error state offers an enabled “Dismiss” recovery control. `Image`, web/HTML, URL, style, arbitrary `Row`, custom templates, and payload-selected color/font/layout are deliberately unsupported until a source-audited profile extension is implemented and tested.

## State, focus, and responsive policy

| State | Profile behavior | Focus / input |
|---|---|---|
| Loading | Profile-owned text skeleton and noninteractive progress label; no stale prior surface remains annotated. | Focus moves to the enabled Cancel/Dismiss recovery only when present; otherwise the app root holds focus safely. |
| Valid | Semantic component tree is rendered in document order, preserving hierarchy rather than flattening title/body fields. | Initial focus is first enabled command/input; visual focus uses both contrast outline and 1.02 scale/elevation. |
| Selected/pressed | Only components with a registered semantic selection/action may become selected. | Pointer requires Down then Up inside; interrupted/leave cancels; key and pointer share reducer command. |
| Disabled | Muted text/surface plus opacity and no focusability; never color-only. | Skipped by directional traversal and activation. |
| Invalid / mismatch / oversize / privacy | Profile-owned error title, bounded reason, and recovery command; untrusted source is not partially rendered. | Recovery is initial focus and Back dismisses it, restoring prior valid focus if one exists. |
| Unsupported | Same safe recovery surface with typed unsupported reason; no generic-card fallback. | Same as invalid. |

NUI uses an inset-aware 1920×1080 reference canvas with one centered uniform ancestor transform. Parsing is cancellable off the UI thread. Only a completed request matching the current monotonically increasing request ID can publish a visible tree. Native bounds are measured after layout and must be finite and positive before publishing a `Tizen.Entity.View`.

## Annotation and A2UI round trip

Each currently visible meaningful component has a stable per-surface View ID (`display:<surface-id>:<component-id>`). The enclosing `Tizen.Entity.View` owns measured `ScreenBounds`/`WindowBounds` and actual `IsFocused`; nested `Annotation` carries `EntityType`, `EntityId`, and generated `Presentation.ToJson()` in `EntityInfo`. Snapshot content excludes obscured values, unrendered source fields, and parser diagnostics that could disclose raw payloads.

`View_ToPresentation` reconstructs separate `surfaceUpdate` and `dataModelUpdate` JSON from the accepted semantic tree, not the raw incoming payload. It preserves surface ID, supported node order, allowed properties, bounded resolved current values, enabled/selected state, and profile version, while remaining semantically equivalent to the rendered tree.

For the current compatibility profile this reconstruction intentionally remains legacy v0.8 so existing consumers are not broken. Canonical v0.9.1 publication, when added, must emit its own ordered lifecycle messages and catalog declaration through a distinct adapter/contract; v1.0 remains Candidate and cannot be advertised as stable.

## Evidence status

- **Protocol source audit:** current v0.9.1 lifecycle/catalog baseline and v1.0 Candidate status inspected; canonical v0.9.1 parser conformance is not implemented.
- **Legacy compatibility:** current `surfaceUpdate` / `dataModelUpdate` parser and serializer are bounded host-tested compatibility behavior, not official v0.9.1 conformance.
- **Renderer source audit:** complete for the v0.1 Samsung One UI adaptation references above.
- **Executable browser preview:** source/parse structure and JavaScript syntax checked; visual browser/console verification remains pending because this worker image has no browser automation runtime.
- **Cross-app fixtures:** Browser currently emits an empty component surface and non-profile document shape; PhotoGallery has no Presentation producer. Neither may yet be used as positive v0.1 evidence.
- **Native/Aurum parity and target round-trip:** not yet verified; tracked in `UI_PARITY.md`. The app now publishes a lock-protected View snapshot only after `CalculateScreenPositionSize()` yields finite positive geometry; the generated View provider maps discovery and `View_ToPresentation` back to that exact current generated Presentation snapshot. Invalid/unsupported inputs instead render a profile-owned `Dismiss` recovery control; dismissing never restores a prior payload. This is host-build evidence, not target evidence.
- **Transparent overlay:** unverified. No claim may be made from an ARGB8888 buffer, host compile, `WindowMode.Transparent`, `SetTransparency(true)`, or transparent View/root color. Required evidence is a Common Emulator native screenshot sequence over a known underlying app (fully transparent, semitransparent, and opaque regions), plus D-pad/key focus, pointer/touch inside/outside declared hit regions, Back/dismiss, focus restoration, pause/resume, and opaque-fallback traces.
