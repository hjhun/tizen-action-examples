# One UI Product UI and Executable HTML Contract

This policy applies to every Tizen NUI product example in this repository.

## 1. Product quality comes before token economy

Do not reduce architecture analysis, One UI research, prototype fidelity, implementation, tests, target validation, or screenshot comparison to save model tokens. Keep work bounded for correctness, but choose the amount of work required to produce a credible product.

## 2. Android Samsung stock apps are the primary UI reference

Before designing an app surface:

1. Name the primary user task and surface type.
2. Select the matching **Android Samsung stock application** as the primary reference wherever one exists. Examples include Samsung Internet for Browser and Samsung Gallery for PhotoGallery. For generic components and system states, inspect the relevant current Samsung Android applications and One UI system surfaces rather than inventing an app-specific visual language.
3. Inspect official Samsung screenshots/help, an available current Android Samsung app, or official One UI guidance. Record the app, screen, source, One UI/app version when known, access date, and which behavior was directly verified.
4. Extract the Samsung app's information architecture, navigation, content hierarchy, component family, typography posture, spacing, shape, state treatment, dialogs, empty/loading/error behavior, and interaction model. Reuse the design principles and recognizable Samsung component behavior without copying trademarks, account data, copyrighted media, or proprietary assets.
5. Adapt the Android touch layout to Tizen NUI viewport, safe areas, remote/D-pad, keyboard, pointer, and touch. Preserve the Samsung app's mental model and component hierarchy; do not merely stretch a phone screenshot or replace it with TV-style invented chrome.
6. Use a non-Samsung Android or iOS reference only when no relevant Samsung application or One UI system surface exists. Document why the fallback was necessary and how it was translated back into One UI conventions.

App-specific baselines for the active batch:

- Browser: current Android Samsung Internet information architecture, navigation, address/search, tabs, loading/error, and privacy-state patterns.
- PhotoGallery: current Android Samsung Gallery Pictures/albums/search/detail/selection/delete-confirmation patterns.
- DisplayPresentation: current One UI component behavior synthesized from Android Samsung stock apps appropriate to each semantic A2UI component; it must not invent a separate DisplayPresentation visual brand.

## 3. HTML is an executable application sample

The required file is `<App>/refs/one-ui-sample.html`.

It is not a requirements document, mood board, style board, architecture report, or a gallery of disconnected mockups. Product requirements belong in Markdown. The HTML must behave like a browser-hosted preview of the NUI application that will be built.

The sample must:

- open directly in a browser without a build step;
- use one app-sized canvas rather than a surrounding design-document page;
- show the same information architecture, visible controls, labels, hierarchy, component states, and content density intended for NUI;
- implement the primary flow and representative loading, empty, error, offline, disabled, confirmation, and success states;
- support pointer/touch activation and keyboard emulation of D-pad, Enter, and Back;
- display initial focus, directional movement, focus restoration, selection, disabled state, and modal focus trapping;
- scale from the same reference canvas and inset policy planned for NUI;
- use local, bounded, privacy-safe fixture data and local placeholders; do not depend on remote stock media, fake accounts, fabricated weather/profile data, or proprietary assets;
- expose an in-sample state switcher only when needed for verification, clearly separated from the product canvas and hidden in normal preview mode;
- contain no architecture prose, acceptance tables, “design contract” cards, or implementation commentary inside the app canvas.

Every visible HTML control must map to one of:

- an implementable NUI component;
- a real WebView/media/renderer surface;
- a domain state or command;
- a typed Action or ViewAnnotation/A2UI interaction.

Record that mapping in `<App>/docs/UI_PARITY.md`, not inside the application sample.

## 4. One UI guardrails

A label saying “One UI” is not evidence of One UI fidelity.

Reject or explicitly justify:

- arbitrary gradients, glassmorphism, floating docks, oversized rounded cards, decorative statistics, and generic dashboard tiles;
- invented navigation that conflicts with the selected reference app;
- desktop web chrome presented as a TV/mobile One UI pattern without adaptation evidence;
- color-only focus, selection, error, or disabled states;
- decorative controls that have no NUI implementation or command semantics;
- a static screenshot that cannot exercise the primary flow.

Use content-first hierarchy, restrained platform-appropriate color, readable type, intentional whitespace, predictable Back behavior, visible recovery actions, and at least two focus cues. Adapt for Tizen remote/D-pad, keyboard, pointer, and touch.

## 5. HTML-to-NUI parity loop

UI development uses an evidence loop, not a one-time handoff:

1. Audit the selected reference and record source-backed rules.
2. Build and browser-verify `one-ui-sample.html`.
3. Capture the sample at each required viewport and state.
4. Implement the same vertical slice in NUI.
5. Install the current package and reach the equivalent state using real input.
6. Capture a native Aurum screenshot.
7. Compare HTML and NUI side by side for hierarchy, geometry, type scale, spacing, color, component state, focus, labels, content density, and responsive behavior.
8. Record pass/difference/intentional-deviation in `docs/UI_PARITY.md` with both image paths.
9. Correct unexplained differences before advancing the UI slice.

A host geometry test, HTML screenshot, or static NUI render cannot substitute for installed target evidence.

## 6. HTML inventory and removal

Keep one canonical executable sample per app. Remove obsolete concept pages, design-document HTML, duplicate variants, remote-asset explorations, and files that no longer map to the actual NUI product. Preserve prior files only when the Goal explicitly records continuing product value and ownership.

## 7. Presentation and A2UI are mandatory together

An app supports DisplayPresentation when it exposes or consumes a `Presentation`, implements a domain `ToPresentation` Action, implements `View_ToPresentation`, advertises integration with `DisplayPresentation`, or renders provider-produced Presentation content.

For every such app, A2UI is mandatory:

- produce separate valid `surfaceUpdate` Template JSON and `dataModelUpdate` Document JSON;
- derive both from the same current generated Entity snapshot and rendered state;
- represent current content, loading/error state, focus/selection, and available controls where applicable;
- never return a canned fixture unrelated to the current UI;
- validate bounds, schema, supported components, malformed/oversized input, and privacy limits;
- verify app Action → Presentation → DisplayPresentation render and ViewAnnotation → `View_ToPresentation` round trips on the Common Emulator;
- capture the rendered DisplayPresentation state and focused/annotated source state with Aurum.

### DisplayPresentation is the Samsung One UI A2UI renderer

`DisplayPresentation` must not flatten A2UI into an arbitrary title/body card or apply a self-invented skin. It is the repository's reference renderer for presenting bounded A2UI through Samsung One UI-adapted Tizen NUI components.

Its architecture must separate:

1. untrusted A2UI parsing and bounded semantic component tree;
2. a versioned supported-component/profile matrix;
3. deterministic semantic A2UI → One UI NUI component mapping;
4. One UI design tokens and responsive/focus policy;
5. NUI rendering, input dispatch, ViewAnnotation, and A2UI round-trip publication.

For every supported A2UI component and variant, the profile records accepted properties and bindings, corresponding NUI component, One UI hierarchy/type/spacing/shape/elevation treatment, disabled/loading/error/selected/focused behavior, D-pad/keyboard/pointer semantics, privacy bounds, and unsupported-property result. Presentation payloads provide bounded semantics and data; they cannot inject arbitrary colors, fonts, scripts, HTML, remote assets, or unbounded layout that bypasses the Samsung One UI profile.

`DisplayPresentation/refs/one-ui-sample.html` must be an executable preview of that renderer. It must accept the same bounded local A2UI Template/Document fixtures used by tests, render them through the same documented One UI profile, and allow primary input/focus/state transitions. A prose page or a hand-authored card that bypasses A2UI parsing is invalid.

The minimum product evidence includes:

- a source-audited A2UI component/profile matrix in `DisplayPresentation/docs/A2UI_ONE_UI_PROFILE.md`;
- reusable One UI NUI renderer components and token mapping rather than per-payload custom layouts;
- positive render fixtures generated by at least Browser and PhotoGallery, not DisplayPresentation-only canned content;
- malformed, mismatched, oversized, unsupported-component/property, stale-request, and privacy-bound failures;
- side-by-side HTML/NUI comparison for each supported component/state and at least two real cross-app Presentation flows;
- Common Emulator `Show` rendering, focus/input, ViewAnnotation, `View_ToPresentation`, and round-trip equivalence evidence.

An app cannot be complete while its advertised Presentation/A2UI path is missing, static, stale, malformed, unverified, or rendered outside the Samsung One UI profile.

## 8. Acceptance gate

Before UI completion, evidence must show:

- authoritative reference and adaptation decisions;
- browser-verified executable sample with primary and exceptional states;
- current package installed and rendered;
- real D-pad/keyboard/pointer behavior and focus restoration;
- state-by-state HTML/NUI comparison with unresolved differences closed;
- Action, Entity, resolver, ViewAnnotation, and mandatory A2UI E2E;
- validated native screenshots in `docs/images/` and linked bilingual documentation.
