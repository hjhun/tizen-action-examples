# Browser Samsung Internet Modernization V2

Date: 2026-08-09
Status: User-approved direction A
Scope: Browser `refs` contract and matching Tizen NUI presentation

## Decision

The approved UI direction is **A — compact top address/search surface plus a separate bottom navigation dock**.

The objective is not a pixel clone of an Android phone. It preserves Samsung Internet's recognizable information architecture and component family while translating density, safe areas, focus and input to a 1920×1080 Tizen Common/TV surface.

## Problems confirmed from the previous installed build

1. Home used an oversized 15-point NUI headline and excessive empty space, so it read as a demo or kiosk instead of a browser start page.
2. Tabs used a 14-point title and long full-width rows. The result lacked Samsung Internet's compact preview-card character.
3. The generic `Browser` label, large text controls and placeholder `N` tiles weakened Samsung product identity.
4. Focus outlines were visually heavier than the component hierarchy.
5. Typography was assigned per view rather than from a bounded window-proportional type scale.
6. The close dialog was structurally sound but oversized and visually generic.

## Product hierarchy

### Page, loading, recovery and home

```text
safe canvas
├── compact top bar
│   ├── Internet product mark
│   ├── address/search capsule
│   └── Reload
├── content surface
│   ├── Home quick access, WebView, loading or recovery
│   └── progress indicator when applicable
└── compact bottom navigation dock
    ├── Back
    ├── Forward
    ├── Home
    └── Tabs + count
```

`Menu`, bookmarks, AI and secret-mode controls are not rendered until they map to real runtime commands. Home is a real semantic command: it returns the selected tab to the local privacy-safe start page without network I/O.

### Home

The start page uses one concise heading and a bounded quick-access row rather than a large marketing hero.

- Kicker: `QUICK ACCESS`
- Heading: `Where would you like to go?`
- Supporting copy: one line at 1920×1080, at most two at narrow viewports
- Quick access: Tizen Docs, Tizen.org and New tab
- No marketing copy, privacy status sentence, personal content or remote thumbnail
- Address retains initial focus; Down enters quick access; a second Down reaches the dock

### Tabs

Tabs are a dedicated full-canvas surface.

- Compact Back affordance, `Tabs` title and bounded normal-tab count
- Two-column card grid at 1920×1080; one column when uniform scaling makes two columns unreadable
- Each card has a privacy-safe local preview, title, URL and circular Close
- Selected tab: blue leading rail plus soft blue surface
- Focused card: 3px high-contrast outline plus 1.015 scale
- Selected and focus treatments remain visually distinct
- `New tab` is a compact trailing action, not a detached button in empty space
- No fake Search, More or Close-all command

### Close confirmation

- Centered rounded surface with restrained width
- Title and one-line consequence
- Integrated split action row
- Cancel receives initial focus
- Close uses red text; focused action uses outline and subtle surface, not an oversized blue rectangle
- Back is equivalent to Cancel and restores the invoking Close control

## 1920×1080 geometry

| Element | Metric |
|---|---:|
| Safe canvas inset | 40 px horizontal, 28 px vertical |
| Top bar height | 84 px |
| Product context width | 210 px maximum |
| Address capsule height | 58 px |
| Reload target | 58×58 px |
| Content top/bottom gap | 16/20 px |
| Bottom dock height | 64 px |
| Bottom dock safe offset | 28 px; must not intersect system overlay |
| Dock control target | at least 72×52 px |
| Home quick-access card | 248×108 px target at 1920×1080 |
| Tabs grid gap | 20 px |
| Tab card height | 190–224 px |
| Dialog width | 620–700 px |

The existing centered uniform ancestor transform remains canonical. No child may apply a second scale.

## Typography contract

HTML uses CSS pixels at the 1920×1080 reference canvas. NUI uses calibrated point sizes that must be checked against native pixel bounds; point values are not accepted by source inspection alone.

| Role | HTML target | NUI initial calibration | Constraints |
|---|---:|---:|---|
| Product label | 24 px | 4.0 pt | semibold, single line |
| Address text | 24 px | 4.0 pt | regular, ellipsized |
| Kicker | 16 px | 2.7 pt | bold, tracked |
| Home title | 52 px | 8.5 pt | semibold, max one line |
| Home/body copy | 26 px | 4.3 pt | regular, max two lines |
| Quick-access title | 24 px | 4.0 pt | semibold |
| Quick-access URL | 18 px | 3.0 pt | regular, muted |
| Dock/icon label | 22 px | 3.7 pt | medium; icon-first |
| Tabs screen title | 52 px | 8.5 pt | semibold |
| Tab title | 28 px | 4.7 pt | semibold, one line |
| Tab URL/count | 20 px | 3.3 pt | regular, muted |
| Dialog title | 38 px | 6.3 pt | semibold, one line |
| Dialog body/action | 22 px | 3.7 pt | regular/medium |

Font fallback order is `SamsungOne`/Samsung system sans when available, then `Noto Sans`, then platform default. The application must not bundle or redistribute proprietary Samsung fonts.

Acceptance requires native screenshot measurement and visual comparison; the initial NUI calibration may be adjusted when rendered glyph bounds differ from HTML.

## Visual tokens

- Background: warm neutral `#F7F7FA`
- Content/card: `#FFFFFF`
- Primary text: `#17171B`
- Secondary text: `#66666F`
- Border: `#DEDEE5`
- Samsung-like blue accent: `#0B76E8`
- Focus soft surface: `#E9F3FF`
- Destructive: `#E3262E`
- Corner radii: 18 px controls, 24 px cards, 28 px dialogs
- Shadows: reserved for floating dock/dialog; no glassmorphism or decorative gradients

## Input contract

- Initial focus: Address
- Top row: Address ↔ Reload
- Down from top: current content action or WebView
- Down from content: dock
- Dock: Back ↔ Forward ↔ Home ↔ Tabs
- Up from dock: content; Up again: Address
- Tabs: Back → row-major cards/open-close → New tab
- Pointer/touch and D-pad activation use the same semantic command
- Disabled commands are skipped
- Modal traps directional focus and restores the invoking control

## Implementation order and test gates

1. Add failing Playwright assertions for approved hierarchy, type tokens, Home command, Tabs grid and focus states.
2. Update `Browser/refs/one-ui-sample.html`; run Playwright and capture Home/Page/Tabs/Dialog at the required viewports.
3. Add failing host tests for NUI render metrics/focus graph/Home command before production changes.
4. Update NUI render policy and views without modifying generated source.
5. Run focused host tests, all Browser executable tests, clean Browser solution build and `git diff --check`.
6. Build with explicit `emulator-test-only` signing, inspect TPK, update-install and launch on Common Emulator.
7. Use Aurum remote, pointer and touch input to reproduce Home/Page/Tabs/Dialog; capture fresh 1920×1080 images.
8. Compare Samsung reference → HTML → NUI for hierarchy, density, typography, geometry and focus. Record deviations in `UI_PARITY.md`.
9. Send Telegram reports after HTML completion, NUI/host completion and installed UI automation completion. Attach native images at the final stage.

## Acceptance criteria

- The previous oversized 15/14/10-point roles no longer appear in Home/Tabs/Dialog.
- Home reads as a compact browser start page, not a marketing hero.
- Chrome and dock do not dominate the WebView.
- Tabs use recognizable preview-card hierarchy and balanced window density.
- No text clipping at 1920×1080, 1280×720, 1440×1080 or 2560×1080 HTML viewports.
- Installed Common Emulator Home/Page/Tabs/Dialog visually match the approved HTML hierarchy.
- Dock remains outside the system Back/Home overlay.
- Focus, selected, disabled and destructive states are distinguishable without color alone.
- Existing navigation, persistence, cancellation and privacy tests remain green.
- Existing RPC/A2UI/accessibility-tree blockers remain explicitly separate and are not reported as fixed by this visual slice.
