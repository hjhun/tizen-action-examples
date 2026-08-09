# DisplayPresentation Architecture Contract

## Product boundary

DisplayPresentation is a deterministic, in-process Tizen NUI renderer for a provider-produced `Tizen.Entity.Presentation`. Its only public Display contract is `Tv_Tizen.Action.Display_Show`: the input is a `Tizen.Entity.Presentation` and the output is `Tizen.Entity.Status`. It is an infrastructure fixture, not a document browser, network client, or persistence product.

A Presentation contains two strings: `Template` and `Document`. Both are required to be bounded, valid JSON objects before they can be rendered. The supported interoperable pair is A2UI: `Template` contains a `surfaceUpdate` object and `Document` contains the matching `dataModelUpdate` object. The renderer accepts a deliberately small, documented subset sufficient for title, subtitle, body, and key/value fields; unknown but valid A2UI components produce a typed unsupported state instead of an invented UI.

## Requirements and acceptance

| Area | Observable requirement |
|---|---|
| Rendering | Show a deterministic content-first screen for a bounded supported A2UI Presentation. |
| Invalid input | Empty, oversized, malformed JSON, missing required A2UI root, or mismatched surface/document produces a typed failure and a visible safe error state. |
| Unsupported input | Valid but unsupported components produce `Success=false` and an explanatory visible unsupported state. |
| Responsiveness | Parsing runs off the UI thread, is cancellable on superseding `Show` and termination, and stale completions never replace newer content. |
| UI | A 1920×1080 design canvas is inset-aware and uniformly transformed once; invalid transient viewport retains the prior frame. |
| Input | Initial focus is the rendered content card; D-pad/keyboard retain deterministic focus, Enter expands/collapses supported details, pointer/touch activates the focused card, and Back returns from detail/error to the last valid presentation (or exits only when no such state exists). |
| Agent context | The currently rendered card is published as one visible `Tizen.Entity.View` with finite positive measured bounds, actual focus, and `Annotation.EntityInfo` from the generated Presentation `ToJson()` output. |
| Interoperability | `View_ToPresentation` returns separate valid A2UI `Template` and `Document` JSON values reconstructed from the same published generated Presentation snapshot. |

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
- `DisplayPresentation.ViewActionProvider` is generated from the complete `Tizen.Internal.Action.View` category. It reads an immutable, lock-protected visible snapshot registry and does not retain NUI views.

## Contract trace

Live catalog inspection establishes the following exact contracts:

- `Tv_Tizen.Action.Display_Show` is the sole entry in the `Tizen.Action.Display` `action.seq` section. Its input is `Tizen.Entity.Presentation`; output is `Tizen.Entity.Status`; the platform default provider is not reused by this app.
- `Tizen.Entity.Presentation` declares `Template` and `Document` strings.
- `Tizen.Entity.View.Annotation` uses `EntityType`, `EntityId`, and **`EntityInfo`**. `ScreenBounds` and `WindowBounds` are properties of the enclosing View.
- The View category provides `FindById`, `GetAnnotatedViews`, `GetFocusedView`, and `ToPresentation`; all will be generated as a complete category and checked against a fresh `actionc -a` output. Generated files are never hand edited.

The visible card uses View ID `display:presentation:active`, EntityType `Tizen.Entity.Presentation`, and a deterministic EntityId derived from the accepted immutable snapshot. The EntityInfo payload is the generated Presentation DTO `ToJson()` result, not a handwritten JSON projection. Bounds are only published after `CalculateScreenPositionSize()` gives finite positive values.

## Design alternatives and decision

1. **Generic arbitrary JSON renderer.** This would make a superficially flexible viewer, but cannot safely define layout, input, privacy, or deterministic failure behavior for unknown schemas.
2. **A bounded A2UI subset renderer (selected).** It interoperates directly with View `ToPresentation`, has a small testable grammar, and gives typed malformed/unsupported outcomes.
3. **A static fixture-only screen.** This would test NUI mechanics but would not render the provider-produced capability named by the product goal.

The selected bounded A2UI subset is real Presentation consumption, while unsupported A2UI is visibly and programmatically rejected rather than simulated.

## Security, lifecycle, and verification

Presentation strings are treated as untrusted data: no HTML/WebView execution, URLs, scripts, arbitrary files, or platform commands are interpreted. Displayed strings are bounded and escaped by NUI text rendering. The app stores only the currently accepted in-memory snapshot and clears the View registry on pause/termination.

Host tests cover parser limits, JSON/A2UI validation, deterministic render-plan mapping, mismatch/unsupported outcomes, viewport geometry, cancellation/stale suppression, and snapshot ordering. Build/package, installed Common Emulator Action/View wire calls, rendered focus/input, Aurum screenshots, and TV/product validation are separate gates; none is implied by this architecture artifact.
