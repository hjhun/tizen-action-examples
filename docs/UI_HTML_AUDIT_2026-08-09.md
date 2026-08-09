# UI HTML Audit — 2026-08-09

## Decision

Autonomous UI development was paused after the user identified that generated screens were being described as One UI without sufficient reference fidelity. The existing HTML inventory was audited and removed. New UI work must follow `ONE_UI_PRODUCT_UI_POLICY.md`.

## Removed inventory

| File | Ownership before audit | Audit result | Removal reason |
|---|---|---|---|
| `Browser/refs/browser-design.html` | pre-existing untracked exploration | Not canonical | Remote stock assets, fabricated weather/profile/content, decorative gradients/glass/floating dock, and a flow that did not map to the current NUI implementation. |
| `Browser/refs/one-ui-design.html` | tracked generated file | Requirements document rendered as HTML | No application canvas, controls, interaction script, screen transitions, or executable focus/state model. |
| `PhotoGallery/refs/photo-gallery-design.html` | adopted tracked exploration | Not canonical | Remote stock assets, arbitrary coral/glass/floating-dock styling, fabricated metadata, and controls outside the bounded product scope. |
| `PhotoGallery/refs/one-ui-design.html` | tracked generated file | Mostly requirements document with static illustrations | The illustrated screens did not form an executable app flow or prove NUI component/state parity. |
| `DisplayPresentation/refs/one-ui-design.html` | tracked generated file | Requirements document rendered as HTML | No A2UI parsing, semantic component tree, One UI component mapping, input/focus behavior, or renderer state transitions. |

## Implementation gaps found

- Browser currently composes a full-window system WebView. The previous HTML depicted extensive browser chrome, tabs, quick launch, profile/weather, and dock surfaces that were not implemented or verified.
- PhotoGallery has domain/media-query seams but no installed NUI product screen evidence. Its previous HTML therefore could not be treated as implementation parity.
- DisplayPresentation has a bounded initial A2UI parser that recognizes `Column` and path-bound `Text`, but no product-complete Samsung One UI A2UI component profile or installed renderer evidence. Flattening values into title/subtitle/body/fields is an initial safety seam, not the target renderer architecture.

## Re-entry gate

Each app must now create exactly one `refs/one-ui-sample.html` that executes the actual planned app flow using implementable controls and local fixtures. Each app must maintain `docs/UI_PARITY.md` and compare every approved sample state to a native screenshot from the installed package.

DisplayPresentation has an additional gate: its sample and native app must parse the same A2UI fixtures and render the semantic component tree through a versioned Samsung One UI NUI profile. Browser and PhotoGallery outputs are required cross-app fixtures.
