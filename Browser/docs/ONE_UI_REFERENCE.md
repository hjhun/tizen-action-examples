# Browser One UI reference and Tizen adaptation

## Scope and source record

- **Primary task:** open a known URL or search, understand the current page, and recover from a failed load without losing control of the browser.
- **Surface:** **Operate** — browser controls and current-page context take precedence over a marketing hero or card dashboard.
- **Reference:** Samsung Internet, observed through Samsung's official product site (`https://samsunginternet.com/`) and Samsung Support's current Galaxy Internet guidance. Research date: 2026-08-09.
- **Supporting implementation evidence:** the live Browser architecture and `Tizen.NUI.BaseComponents.WebView` adapter in this repository establish a single, real system WebView canvas, asynchronous URL navigation, cancellation, bounded failure text, and the current public Browser page projection.

The sources establish Samsung Internet as the applicable Samsung browser reference and the project source establishes the target runtime boundary. They do not license its brand, iconography, proprietary page content, or phone-only interaction patterns. This document records the adaptation rather than cloning a Samsung screen.

## Verified reference model and adaptation

| Reference pattern | Tizen Browser adaptation | Why |
|---|---|---|
| Samsung Internet separates the top address/search + Reload area from its bottom navigation controls. | A compact top address/search surface keeps Reload adjacent, while a centered bottom dock contains only the implemented Back, Forward, and Tabs commands. The URL is not repeated in another chrome row. | Preserves the source hierarchy, shortens the remote path, and avoids generic desktop top chrome. |
| Page content is the primary surface, not a collection of browser-dashboard cards. | The real `WebView` is the largest region. The sample uses bounded local page fixtures solely to demonstrate its planned runtime states. | The Browser product gate requires a real target web engine; fixtures cannot replace it. |
| Navigation controls become unavailable when there is no applicable history/load operation. | Disabled Back/Forward are visibly muted and not activatable; Reload remains available for recoverable errors. | Makes state and D-pad focus deterministic. |
| Tabs are a separate management surface with preview, title, URL, selected cue, circular close, and New tab. | Tabs owns the full reference canvas and uses bounded vertical cards with local preview tiles, separate title/URL, a leading selected rail, circular close, and New tab. | Retains the Samsung tab-management family without stretching a phone screenshot or inventing a TV dashboard. |
| Failed connectivity/loading has a concise explanation and a direct recovery path. | Loading, offline, engine-error, and close-tab confirmation are first-class states. Retry returns focus to the address field or Retry button. | The WebView adapter already maps bounded timeout/load errors; no silent freeze is acceptable. |

## Information architecture and control hierarchy

1. **Browser workspace (root):** compact address/search + Reload → page/recovery content → centered Back/Forward/Tabs dock.
2. **Tabs manager (secondary):** full-canvas header → ordered preview/title/URL cards → New tab.
3. **Recovery overlay (exception):** concise reason and Retry/Back; it traps focus until dismissed.
4. **Close-tab confirmation (destructive exception):** Cancel and Close tab; Back cancels and restores the invoking focus.

No profile, weather, quick-launch, floating dock, synthetic statistics, remote imagery, or account controls are part of this product. Bookmark/history commands remain a planned later Browser slice and are intentionally not rendered before their NUI/use-case mapping exists.

## Tizen input and scaling policy

- The NUI implementation will use one inset-aware, centered uniform 1920×1080 reference canvas transform. The sample uses the same logical canvas/aspect-ratio policy and scales within the browser viewport.
- Initial focus is the address/search input. Left/Right moves between Address and Reload. Down crosses content to the Back/Forward/Tabs dock; Up returns through content to Address. Tabs uses Back, ordered open/close pairs, and New tab. Enter activates. Keyboard `Enter`, arrows, and `Escape` emulate Enter, D-pad, and Back. Pointer/touch uses the same command reducer.
- Back first dismisses the active modal, then leaves Tabs, then invokes browser-history Back only when available. Focus restoration is explicit: Tabs returns to Tabs; cancelled confirmation returns to the tab row; recovery returns to Retry or address depending on its source.
- Focus is not color-only: the active control has a high-contrast outline plus subtle scale/elevation. Disabled controls are excluded from keyboard focus.

## State inventory

| State | Trigger | User-visible recovery |
|---|---|---|
| Home | launch / empty tab | address field and a bounded local start page |
| Loading | submitted URL/search | progress band; navigation commands update availability |
| Page | successful WebView load | title, safe URL context, active content surface |
| Offline | reachable engine reports offline/load failure | Retry and Back |
| Engine error | WebView cannot start or page fails | Retry and Back |
| Tabs | Tabs command | select, create, or request close of a normal-mode tab |
| Close confirmation | Close tab | Cancel or confirm Close; focus restoration |

## Evidence boundary

The HTML sample and installed Common Emulator package now have separate visual evidence in `UI_PARITY.md`. That evidence proves Browser chrome, a real public WebView page, and bounded remote/pointer/touch flows at 1920×1080; it is not TV-product approval. Aurum still returns an empty semantic tree, and the generated-provider ABI blocker continues to prevent typed Action/View RPC, annotations over RPC, and legacy Presentation round trips. Canonical A2UI target transport remains independently blocked.
