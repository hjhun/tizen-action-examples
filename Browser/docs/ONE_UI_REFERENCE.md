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
| A browser keeps the current address/page context visibly available while people navigate. | A persistent top command band shows Back, Forward, Reload, an editable address/search field, and Tabs; the current URL is repeated as small page context after a load. | A remote user must be able to recover navigation without hidden gestures. |
| Page content is the primary surface, not a collection of browser-dashboard cards. | The real `WebView` is the largest region. The sample uses bounded local page fixtures solely to demonstrate its planned runtime states. | The Browser product gate requires a real target web engine; fixtures cannot replace it. |
| Navigation controls become unavailable when there is no applicable history/load operation. | Disabled Back/Forward are visibly muted and not activatable; Reload remains available for recoverable errors. | Makes state and D-pad focus deterministic. |
| Tabs are a separate management surface, rather than permanent content competing with the page. | Tabs opens a compact full-screen manager with ordered normal-mode tabs and Close controls; closing returns focus to Tabs or the address field. | Fits TV focus navigation and avoids a mobile bottom navigation bar. |
| Failed connectivity/loading has a concise explanation and a direct recovery path. | Loading, offline, engine-error, and close-tab confirmation are first-class states. Retry returns focus to the address field or Retry button. | The WebView adapter already maps bounded timeout/load errors; no silent freeze is acceptable. |

## Information architecture and control hierarchy

1. **Browser workspace (root):** command band → address/search field → current-page context → WebView surface.
2. **Tabs manager (secondary):** ordered normal-mode tab rows, selected state, close command, and New tab command.
3. **Recovery overlay (exception):** concise reason and Retry/Back; it traps focus until dismissed.
4. **Close-tab confirmation (destructive exception):** Cancel and Close tab; Back cancels and restores the invoking focus.

No profile, weather, quick-launch, floating dock, synthetic statistics, remote imagery, or account controls are part of this product. Bookmark/history commands remain a planned later Browser slice and are intentionally not rendered before their NUI/use-case mapping exists.

## Tizen input and scaling policy

- The NUI implementation will use one inset-aware, centered uniform 1920×1080 reference canvas transform. The sample uses the same logical canvas/aspect-ratio policy and scales within the browser viewport.
- Initial focus is the address/search input. Left/Right move across the command band; Down enters the page surface or focused tab list; Up returns to the command band. Enter activates. Keyboard `Enter`, arrows, and `Escape` emulate Enter, D-pad, and Back. Pointer/touch uses the same command reducer.
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

This source-backed design record is not native parity evidence. `refs/one-ui-sample.html` demonstrates the planned NUI flow in a browser. Native Browser chrome, real WebView content/loading, Aurum input, annotations, and A2UI remain unverified until the installed package's generated-provider runtime blocker is resolved.
