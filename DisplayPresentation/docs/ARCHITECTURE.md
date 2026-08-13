# DisplayPresentation Architecture Contract

## Product boundary

DisplayPresentation is a deterministic, in-process Google A2UI-compatible renderer for a provider-produced `Tizen.Entity.Presentation`. Google A2UI defines the versioned protocol, lifecycle, catalog, semantic components, data binding, and action contract; Samsung One UI defines the Tizen NUI presentation mapping, visual hierarchy, focus, input, and responsive behavior. Its only public Display contract is `Tv_Tizen.Action.Display_Show`: the input is a `Tizen.Entity.Presentation` and the output is `Tizen.Entity.Status`. It is an infrastructure fixture and shared product surface, not a document browser, network client, persistence product, generic title/body card, repository-specific A2UI dialect, or arbitrary payload-skinnable canvas.

A Presentation contains two strings: `Template` and `Document`. Both are required to be bounded, valid JSON objects before they can be rendered. Existing repository producers place legacy A2UI v0.8-style `surfaceUpdate` in `Template` and matching `dataModelUpdate` in `Document`; this remains an explicit compatibility profile and must not be described as canonical v0.9.1. A version adapter parses the declared wire profile into one bounded semantic component tree, after which the profile validator and renderer are version-independent. Unknown components or unsupported properties produce a typed unsupported state instead of an invented layout. The existing Column/Text parser is only an initial safety seam; it is not product-complete until the source-audited profile, component mapping, cross-app fixtures, and native renderer evidence exist.

Canonical upstream is <https://github.com/a2ui-project/a2ui>. Revision [`ec97cb0d7499932e67003ffe5b709a3db7e7033a`](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a), committed 2026-08-07 and inspected 2026-08-09, identifies v0.9.1 as **Current Production** and v1.0 as **Candidate**. Canonical v0.9.1 uses `createSurface`, `updateComponents`, `updateDataModel`, and `deleteSurface`, with client `action` messages and a negotiated `catalogId`; v1.0 is not treated as stable. The renderer owns styling/native components. A2UI defines a logical surface lifecycle and does not define an OS-level transparent window or 32-bit framebuffer.

## Requirements and acceptance

| Area | Observable requirement |
|---|---|
| Rendering | Parse a bounded A2UI semantic tree and render every supported component through the versioned Samsung One UI NUI profile. |
| Invalid input | Empty, oversized, malformed JSON, missing required A2UI root, or mismatched surface/document produces a typed failure and a visible safe error state. |
| Unsupported input | Valid but unsupported components or properties produce `Success=false` and an explanatory visible unsupported state; no guessed or generic fallback layout. |
| Responsiveness | Parsing runs off the UI thread, is cancellable on superseding `Show` and termination, and stale completions never replace newer content. |
| UI | A 1920×1080 design canvas is inset-aware and uniformly transformed once; reusable One UI tokens/components provide hierarchy, typography, spacing, shape, state, and focus; payloads cannot inject an arbitrary skin. |
| Input | Focus and actions derive from the semantic component tree and supported profile; D-pad/keyboard/pointer/touch use one command path, modal/detail Back restores the invoking component, and no state creates a focus dead end. |
| Agent context | Currently rendered meaningful A2UI components publish visible `Tizen.Entity.View` snapshots with finite positive measured bounds, actual focus, and canonical generated Entity context. |
| Interoperability | `View_ToPresentation` returns separate valid A2UI `Template` and `Document` JSON values reconstructed from the same published generated Presentation snapshot; Browser and PhotoGallery fixtures render equivalently through `Show`. |
| Window hosting | Semantic rendering works in the mandatory opaque full-window mode. Transparent overlay mode is enabled only by an explicit capability result and falls back to opaque on absence/failure. |
| Overlay proof | Installed Common Emulator screenshots prove underlying-app compositing through fully transparent regions and correct semitransparent/opaque rendering; input traces prove D-pad/key focus, pointer/touch hit regions, Back/dismiss, focus restoration, pause/resume, and fallback. |

Collection/input limits are: 64 KiB per JSON string, 32 rendered fields, 256 characters per displayed string, and one active rendering request. No presentation payload is persisted; process restart starts empty.

## Architecture

```text
NUI App / Display Service / View Service
             |                 |
             v                 v
       RenderCoordinator <- VisiblePresentationRegistry
             |
             v
 A2UI PresentationParser + RendererPlan (Tizen-free use cases)
             |
             v
 Presentation domain value + bounded validation (Tizen-free)
```

- `DisplayPresentation.Domain` holds immutable validated presentation, render-plan, error, and viewport types.
- `DisplayPresentation.UseCases` parses, validates, bounds, and maps A2UI JSON to a renderer plan. It receives a `CancellationToken` and returns a typed outcome.
- `DisplayPresentation.Persistence` contains no payload store: its explicit role is to prevent accidental persistence of provider data.
- `DisplayPresentation.ActionProvider` is a thin whole-category generated `Tizen.Action.Display` adapter. It maps generated Presentation DTOs to the use case and returns initialized typed Status values.
- `DisplayPresentation.App` owns the NUI thread, reference-canvas root, focus, and lifecycle cancellation. It shares one `RenderCoordinator` with providers; it never calls its own RPC.
- A narrow window-host policy selects `Opaque` or capability-gated `TransparentOverlay` before/at window creation and reports the effective mode. The semantic renderer and coordinator do not branch on that mode. Opaque mode is always complete; overlay failure never leaves a blank/noninteractive app.
- `DisplayPresentation.ViewActionProvider` is generated from the complete `Tizen.Action.View` category. It reads an immutable, lock-protected visible snapshot registry and does not retain NUI views.

## Contract trace

Live catalog inspection establishes the following exact contracts:

- `Tv_Tizen.Action.Display_Show` is the sole entry in the `Tizen.Action.Display` `action.seq` section. Its input is `Tizen.Entity.Presentation`; output is `Tizen.Entity.Status`; the platform default provider is not reused by this app.
- `Tizen.Entity.Presentation` declares `Template` and `Document` strings.
- `Tizen.Entity.View.Annotation` uses `EntityType`, `EntityId`, and **`EntityInfo`**. `ScreenBounds` and `WindowBounds` are properties of the enclosing View.
- The View category provides `FindById`, `GetAnnotatedViews`, `GetFocusedView`, and `ToPresentation`; all will be generated as a complete category and checked against a fresh `actionc -a` output. Generated files are never hand edited.

The visible card uses View ID `display:presentation:active`, EntityType `Tizen.Entity.Presentation`, and a deterministic EntityId derived from the accepted immutable snapshot. The EntityInfo payload is the generated Presentation DTO `ToJson()` result, not a handwritten JSON projection. Bounds are only published after `CalculateScreenPositionSize()` gives finite positive values.

## Design alternatives and decision

1. **Generic arbitrary JSON/card renderer.** This would make a superficially flexible viewer, but cannot safely define layout, input, privacy, deterministic failure behavior, or Samsung One UI fidelity.
2. **Official Google A2UI contract → Samsung One UI-adapted NUI renderer (selected).** It interoperates directly with provider and View `ToPresentation`, preserves the declared A2UI version/catalog/semantic structure, maps supported components to reusable One UI NUI components, and has a source-audited testable grammar with typed unsupported behavior. Existing v0.8-style producers remain behind an explicit compatibility adapter rather than defining the canonical contract.
3. **Static fixture-only or title/body screen.** This would test NUI mechanics but would neither render the provider-produced A2UI structure nor prove cross-app interoperability.

The selected profile targets official Google A2UI conformance and Samsung One UI-adapted rendering. Unsupported A2UI is visibly and programmatically rejected rather than flattened, guessed, or simulated. `docs/A2UI_ONE_UI_PROFILE.md`, executable sample parity, Browser/PhotoGallery fixtures, and installed native evidence are mandatory before this architecture can be called implemented.

## Native transparency boundary

Three independent terms must never be collapsed:

1. **32-bit ARGB8888 buffer** means four 8-bit pixel channels. It proves storage precision/layout, not that a compositor preserves alpha.
2. **Per-pixel-alpha/translucent native window** means the OS window/compositor accepts and blends the alpha channel. API10 exposes `NUIApplication(string, WindowMode.Transparent)`, `Window.SetTransparency(true)` (since Tizen 5), and `Color.Transparent`; `Window.SetOpaqueState(bool)` only tells the window manager whether a transparent window is currently visually opaque and does not change its mode. Transparency also does not imply pointer/touch pass-through or focus policy: `SetInputRegion` and `SetAcceptFocus` are separate public controls.
3. **A2UI logical surface** is version/catalog-scoped component and data state with lifecycle updates/actions. It has no pixel-format or OS-window semantics.

The overlay capability probe is target/profile-specific and fail-closed. A host compile, an ARGB8888 texture/buffer, a transparent NUI View/root, or successful invocation of the transparency APIs is only configuration/build evidence. Overlay support remains **unverified** until Common Emulator native screenshots and input/focus traces satisfy the table above. Until then, the effective supported host is opaque.

## Security, lifecycle, and verification

Presentation strings are treated as untrusted data: no HTML/WebView execution, URLs, scripts, arbitrary files, or platform commands are interpreted. Displayed strings are bounded and escaped by NUI text rendering. The app stores only the currently accepted in-memory snapshot and clears the View registry on pause/termination.

Host tests cover parser limits, JSON/A2UI validation, deterministic render-plan mapping, mismatch/unsupported outcomes, viewport geometry, cancellation/stale suppression, and snapshot ordering. Build/package, installed Common Emulator Action/View wire calls, rendered focus/input, Aurum screenshots, and TV/product validation are separate gates; none is implied by this architecture artifact.
