# Samsung Calendar-inspired One UI TV Split Design

**Status:** User-selected visual direction; awaiting written-spec review  
**Selected candidate:** B — One UI TV Split  
**Target:** Tizen NUI Calendar app on 1920×1080-class TV screens  
**Reference:** Official Samsung Calendar Galaxy Store screenshots retrieved 2026-08-08 from `com.samsung.android.calendar`

## 1. Product decision

The Calendar home screen will use Samsung Calendar's familiar month-first information architecture and restrained One UI visual language, adapted to a 16:9 television. It will not make a pixel-for-pixel copy or reuse Samsung trademarks, proprietary icons, or branded assets.

The selected B layout is a persistent split view:

- Left 68%: complete 6×7 month grid.
- Right 32%: agenda for the currently selected date.
- Header: menu affordance, centered month label, search affordance, and Today control.
- The month grid remains the primary navigation surface; the agenda is contextual detail.

This replaces the current concatenated text rendering. Every date and event will be rendered as a distinct NUI view.

## 2. Reference characteristics retained

The design retains the following verified Samsung Calendar characteristics:

- light, low-chrome background and generous whitespace;
- bold centered month abbreviation/title;
- seven weekday columns with Sunday in red;
- leading/trailing month dates shown with reduced contrast;
- subtle rounded date-cell tint rather than heavy grid lines;
- today represented by a dark rounded date pill;
- events represented with short pastel chips and calendar-colored accent bars;
- selected-date agenda with date heading, chronological events, all-day treatment, and empty state;
- clear Today, Search, and Add affordances.

The Tizen app will use neutral line icons or text symbols created for this project. It will not display Samsung branding or imply that it is the Samsung Calendar application.

## 3. 16:9 layout contract

### 3.1 Safe area

The root surface uses a TV-safe inset of 64 px horizontally and 44 px vertically at 1920×1080. All dimensions scale from the window size; 1920×1080 is the reference canvas, not a fixed-only layout.

### 3.2 Header

- Height: approximately 112 px.
- Left: menu/view-mode affordance.
- Center: month abbreviation and year, for example `AUG 2026`.
- Right: Search and Today controls.
- The month title is the strongest typographic element in the header.
- Header controls are focusable only after focus explicitly enters the header; date navigation never accidentally escapes into it.

### 3.3 Month pane

- Width: 68% of the content area.
- Seven equal weekday columns.
- Six equal date rows, always producing 42 cells.
- Weekday header is visually separate from date cells.
- Date cells use 8–12 px equivalent spacing and restrained rounded corners.
- Each cell can show at most two event chips; overflow is represented as `+N`.
- Chips contain a shortened event title when space permits, otherwise a colored line/density indicator.

### 3.4 Agenda pane

- Width: 32% of the content area.
- Surface: subtly different neutral background with a divider from the month pane.
- Header: large day number plus weekday and full date.
- Event list: chronological cards with time, title, optional location, and calendar-color accent.
- All-day events appear before timed events.
- Empty date: a focusable `No events` card; Back returns to the selected date, so the state is not a focus dead end. Add remains visibly disabled until event creation exists.
- More events than fit vertically are scrollable within the agenda without moving the selected date.

## 4. Visual-state contract

The following states are independent and must not be collapsed into one color treatment:

- **Out of month:** reduced text contrast and neutral background.
- **In month:** standard text contrast.
- **Today:** dark date-number pill, independent of focus.
- **Selected date:** persistent pale blue cell tint and selection marker.
- **Focused date:** 3–4 px high-contrast outline, elevated shadow, and approximately 1.03 scale.
- **Date with events:** one or two pastel chips plus `+N` overflow.
- **Focused agenda event:** high-contrast outline and elevation, retaining its calendar color.
- **Disabled/unavailable action:** reduced contrast without looking selected.

Focus must remain understandable in grayscale. Color is supplemental, never the only cue.

## 5. Remote-control interaction

### 5.1 Month navigation mode

- Initial focus: selected date; default is today when visible, otherwise the persisted selected date.
- Left / Right: previous / next calendar day.
- Up / Down: previous / next week, preserving weekday.
- Crossing a month boundary updates the visible month while preserving deterministic focus.
- Enter on a date: transfers focus to the first agenda event for that date.
- Enter on an empty date: transfers focus to the `No events` card.
- Back at the month root: requests application exit.

### 5.2 Agenda navigation mode

- Up / Down: previous / next agenda item.
- Left: returns focus to the same selected date in the month grid.
- Right: no operation in the first slice, avoiding hidden navigation.
- Enter on an event: no operation in this slice. Event detail and event-annotation activation are deferred together, avoiding a control that only appears to navigate.
- Back: returns to the exact selected date cell.

### 5.3 Header navigation

- From the top month row, an additional Up focuses Today; disabled Menu and Search affordances are skipped.
- Down from Today returns to the previously focused date.
- Header Left / Right are no-ops in this slice because Today is the only enabled header control.
- Today activates the current date and updates the visible month.
- Search and menu remain visible to preserve the approved hierarchy, but are disabled and unfocusable in this slice.

## 6. Component boundaries

- `CalendarApplication`: composition root, lifecycle, repository/provider wiring, and top-level key routing only.
- `CalendarUiState`: visible month, selected date, focus region, focused agenda index, and transitions.
- `CalendarMonthView`: responsive header, weekday row, and 42 independent date cells.
- `CalendarDateCellView`: visual state for one date and its event indicators.
- `SelectedDayAgendaView`: selected-date heading, event cards, empty state, and agenda focus.
- `CalendarEventRepository`: unchanged source of domain events and overlap queries.
- `CalendarActionProviderHost`: unchanged provider boundary sharing the same repository.

The UI continues to read domain objects directly. It must not call its own Action provider or use generated provider DTOs as view models. Generated `CalendarActionProvider.cs` remains untouched.

## 7. Data flow

1. Application creates one `CalendarEventRepository`.
2. The same repository instance is injected into the provider host and UI views.
3. Visible month queries use a bounded overlap interval.
4. Selected-date agenda queries use `[dayStart, nextDayStart)`.
5. Domain ordering controls event ordering; the UI does not reimplement resolver semantics.
6. Stable event IDs remain available for future Entity annotation and detail refresh.

## 8. States and failure handling

- **Loading:** month skeleton/date placeholders are displayed without taking focus.
- **Empty day:** agenda empty-state card with Add action.
- **Empty month:** complete date grid remains visible; agenda still follows selection.
- **Repository error:** agenda displays a recoverable error card; month navigation remains usable.
- **Deleted/unresolved event:** removed from the next query without invalidating selected date.
- **Clipping prevention:** title, event title, and location use bounded ellipsis; date number never truncates.
- **Overscan:** no focus outline or primary text touches the window edge.

## 9. Light and dark readiness

The first implemented and emulator-accepted appearance is the Samsung-inspired light theme selected in B. Colors must be provided through named tokens rather than inline per-view constants so a later dark theme can replace the token set without restructuring components.

Minimum tokens include:

- root surface;
- secondary surface;
- cell normal/out-of-month/selected;
- text primary/secondary/disabled;
- Sunday/accent;
- today pill;
- focus outline/shadow;
- event green/blue/orange/purple.

Dark-theme implementation remains outside this slice, but the component structure must not block it.

## 10. Scope of the next implementation slice

Included:

- real responsive NUI split layout;
- 42 independent date cells;
- Samsung-inspired light tokens;
- selected/today/focus/event states;
- agenda cards and empty state;
- D-pad month navigation and month-boundary transitions;
- month-to-agenda focus transfer and exact focus restoration;
- package, install, launch, runtime-log, and `winfo` screenshot acceptance on `emulator-26101`.

Deferred:

- event creation/editor;
- event detail editing;
- Search behavior;
- Month/Week/Day/Schedule menu;
- persistence and real Calendar DB integration;
- Samsung Account integration;
- dark-theme implementation;
- remaining Calendar Action methods;
- physical TV verification.

Deferred controls must not pretend to work. They are either omitted or shown disabled and unfocusable.

## 11. Acceptance criteria

The implementation is accepted only when all of the following are true:

- the emulator screenshot visibly matches candidate B's split hierarchy;
- the month title, weekday headers, 42 independent cells, event chips, and agenda are readable without clipping;
- the current date, selected date, focused date, out-of-month date, and event-bearing date remain visually distinct;
- D-pad movement works across row and month boundaries;
- Enter transfers focus into agenda and Back restores the same date-cell focus;
- an empty date provides a visible, focusable `No events` state from which Back restores the date cell;
- domain, UI-state, and provider focused verification passes;
- NUI build, TPK packaging, install, launch, process survival, and runtime log checks pass;
- a fresh `winfo -dump topvwins` image is compared against candidate B and stored in the durable workspace.

## 12. Explicit deviations from Samsung Calendar

- Landscape split view replaces the mobile portrait transition model.
- D-pad focus ring replaces touch feedback.
- Samsung trademarks, branded icon assets, and proprietary illustrations are not used.
- Search, view-mode menu, event editing, and Add behavior are deferred until implemented rather than represented as functional.
- Entity annotation and Action-provider integration remain Tizen-specific architectural additions outside Samsung Calendar's visible UI model.
