# Browser architect stage

## Scope and product decision

Browser is a living-room web workspace, not a static content mockup. This bounded product owns normal-mode tabs, per-tab navigation history, and the current page projection. Bookmarks, account sync, downloads, private mode, extensions, and AI features are outside this mission. It uses package/application ID `org.tizen.browser`; that identity is distinct from the platform-owned `Tizen.Action.Browser` category and its existing `details.appid` value (`org.tizen.next-browser`). The application registers only the existing Actions it actually implements.

The platform catalogue already supplies `Tizen.Entity.Browser` (`Id`, `Url`, `Title`, `Details`) and five Browser Actions in ABI order: `Tv_Tizen.Action.Browser_GetCurrent`, `Go`, `ToCalendar`, `ToPresentation`, and `GetBrowserByIds`. No custom Action is justified: Go controls navigation, GetCurrent/ordered resolver support discovery and refresh, and the conversion/presentation actions cover the advertised handoffs. Search, Back/Forward, Reload, Home, tab switching, bookmark controls, and focus movement are local UI intents rather than unproven cross-app contracts.

## Feasibility and boundary

The implementation candidate is `Tizen.NUI.BaseComponents.WebView`, whose installed Tizen API reference exposes remote/local content plus `WebEngineType` selection (`UseSystemSetting`, `Chromium`, `LWE`), URL/load/error events, navigation/new-window policy, certificate, HTTP-authentication, and user-media permission hooks. The App will select `UseSystemSetting` initially and place this API behind `IWebRuntime`; it will detect startup/load failure and expose a recoverable unavailable/offline/error state. Actual Common Emulator startup, engine choice, reachable HTTPS navigation, certificate behavior, and keyboard/remote focus remain explicit target gates.

```text
NUI App / WebView runtime adapter / typed Action providers / View provider
                               ↓
                         Browser use cases
                               ↓
             Browser domain (tab, page, history, bookmark, query)
                               ↓
          JSON persistence and runtime/network abstractions
```

- **Domain**: immutable public page snapshots with stable, app-issued IDs; bounded normal-mode tab/history collections; ordered duplicate-preserving ID resolution.
- **UseCases**: bounded URL/search normalization, immutable navigation phase, tab selection, lifecycle restoration, active-request cancellation, stale-result suppression, and typed result mapping. It accepts `CancellationToken` and has no Tizen dependency. Public URL projection removes user info, query, and fragment.
- **Persistence**: one bounded version 2 atomic JSON document for 1~20 normal tabs, nullable public page metadata, selected stable ID, and version 1 page migration. Same-directory temporary replacement publishes at most 256KiB. Tab creation, selection, close, and selected-page workspace updates persist the desired snapshot before publishing it through the tab coordinator. Credentials, query/fragment, cookies, form contents, private mode, and raw page content are never persisted or exposed to Actions/annotations.
- **Web runtime adapter**: creates/disposes the real `WebView`, turns its callbacks into a bounded asynchronous navigation stream, sets a per-navigation timeout, cancels a superseded request, and permits at most one active navigation per selected tab. It marshals only validated final snapshots to the NUI thread.
- **App**: owns NUI composition, inset-aware reference-canvas rendering, focus graph, overlays, and lifecycle. UI and providers receive the same use-case/repository instances; the UI does not call its own RPC.
- **ActionProvider**: generated full-category binding and thin validation/conversion/status adapter. Inputs are bounded before use. `GetBrowserByIds` preserves request order and duplicates and explicitly returns unresolved IDs.
- **ViewActionProvider**: generated full `Tizen.Internal.Action.View` binding over a lock-protected snapshot registry. Only visible normal-mode page/tab cards are published. Each View uses a stable per-surface ID, actual positive finite NUI bounds, actual focused View identity, `Annotation.EntityType = Tizen.Entity.Browser`, and the generated Browser DTO `ToJson()` in `Annotation.EntityInfo`. `View_ToPresentation` creates matching JSON `surfaceUpdate` and `dataModelUpdate` documents from that same JSON.

Stage 2B fixes non-URL search input to the branded HTTPS DuckDuckGo result URL without suppressing provider branding or advertising parameters. The `?q=` form and privacy rationale were directly checked on 2026-08-09 against DuckDuckGo's official [URL parameter guidance](https://duckduckgo.com/duckduckgo-help-pages/settings/params) and [Search Privacy Protection](https://duckduckgo.com/duckduckgo-help-pages/search-privacy). Choosing it as this sample's bounded default is a product adaptation, not a Samsung behavior claim or a partnership claim. Search text remains only in the private WebView request; public Browser metadata removes the query and fragment.

## Product flows and interaction contract

1. **Home / empty**: initial focus is the address input. A user can enter a URL or search phrase, select a bookmark/history result, or open tabs. An empty history/bookmark state explains the next action.
2. **Navigation**: submitting an address creates a cancellable load, renders progress, then updates title/URL and page context on completion. Offline, timeout, invalid URL, certificate, and unavailable-engine conditions provide a concise reason plus Retry or Back; they do not freeze the UI.
3. **Page workspace**: Back/Forward/Re-load/Home have disabled states when unavailable. The selected visible page is the primary annotated surface. Page-originated pop-ups and permission/authentication requests are denied or shown as an explicit non-secret policy state until a target-supported product policy is implemented.
4. **Tabs**: tab manager has deterministic card order. Opening, closing, selecting, and restoring a normal tab are local commands; closing the selected tab restores focus to the nearest remaining card or the address input.
5. **Back hierarchy**: recovery returns to the prior stable page or Home, then page Back moves one real WebView history entry. Stage 2C inserts close confirmation and the Tabs surface above page history.
6. **Calendar handoff**: current schema compatibility is preserved, but extraction remains typed `unavailable`; the app does not invent event data.

Remote/D-pad: Left/Right move within chrome or ordered cards, Up/Down move between chrome/content/dock, Enter activates, and Back closes a modal before returning from a secondary page before browser history. Keyboard follows the same focus order; pointer/touch focuses then activates controls. Every focusable item has an accessible label and visible high-contrast focus state.

## One UI/reference-canvas policy

The physical root paints the full window. A single top-left-pivot 1920×1080 design canvas is uniformly scaled and centered inside `Window.Default.WindowSize` minus `GetInsets()`. All content, overlays, typography, radii, borders, and focus geometry remain design units below that transform. Invalid or non-positive resize/inset measurements retain the prior root. ViewAnnotation uses only measured world bounds from the final NUI View; it does not synthesize bounds from design coordinates.

## Acceptance and test matrix

| Gate | Observable acceptance |
|---|---|
| Host domain/use-case | Stable IDs; resolver ordering/duplicates; URL/input bounds; cancellation/stale-result suppression; timeout/retry mapping; persistence restore; bounded collections; private data exclusion; concurrent snapshot safety. |
| Provider build | Whole categories are generated from live schemas without hand edits; concrete provider implementations compile. |
| Package | Manifest uses `org.tizen.browser`; payload contains App/provider/runtime dependencies; the selected signing mode is explicitly recorded. |
| Action target | Provider discovery with app ID; every advertised Action gets success and bounded-negative RPC; postconditions are checked through GetCurrent/GetBrowserByIds. |
| Web target | A real WebView starts and loads a reachable target-observed page; error and retry are observed separately. |
| View target | Annotated/focused views have parseable generated `EntityInfo`, finite bounds, FindById parity, and valid separate A2UI Template/Document JSON; missing-ID is typed failure. |
| UI/Aurum | Home, load, error/retry, page, tab manager, close confirmation, and focus states have native screenshot postconditions. Common Emulator evidence is reported separately from TV/product validation. |

## Material alternatives considered

1. **Real NUI WebView vs. static HTML/cards.** Static cards make visual development easier but fail Browser product realism and cannot prove navigation. Select real `WebView` behind an adapter; retain deterministic local test fixtures only for tests.
2. **Direct Tizen dependencies in use cases vs. adapter boundary.** Direct calls reduce initial files but make cancellation/persistence/provider behavior device-only. Select a portable domain/use-case core and narrow Tizen adapters.
3. **New custom navigation Actions vs. existing Browser category plus local controls.** Custom Actions would expand Agent surface and require a platform-gap proof. Select existing category; reassess only if a concrete external Agent use case cannot be composed from Go/current/resolver/presentation.

## Risks retained for implementation

- The reference confirms compile-time WebView APIs, not installed-target engine/network behavior.
- A web page title and URL can be exposed, but sensitive content must not enter `Details`, annotations, persistence, A2UI, or screenshots used as fixtures.
- The platform Browser action names retain the `Tv_` prefix; implementation and manifests must use the exact catalogue names, while `org.tizen.browser` remains the sample provider identity.
