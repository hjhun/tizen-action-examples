# Calendar 2.0 Navigation, Views, Search, and Action Design

- Date: 2026-08-08
- Status: Approved visual direction; implementation handoff
- Reference direction: Google/Apple Calendar interaction conventions adapted to Tizen NUI TV
- Approved visual option: A — integrated top command bar
- Browser prototype: generated under `.superpowers/brainstorm/` for the design session; not a product artifact

## 1. Top-level decision

Calendar 2.0 will use one persistent, remote-first command bar for discoverable period navigation, Today, advanced search, and Month/Week/Day/Agenda mode switching, while keeping UI navigation local to the app and extending the public Action surface only where a new typed period-search capability is required.

## 2. Goals

1. Make previous/next navigation visible and usable by pointer and TV remote.
2. Provide Month, Week, Day, and Agenda views with consistent period movement.
3. Provide advanced event search across title, location, and note with an explicit date range.
4. Keep UI state, domain search, persistence, Action providers, and ViewAnnotation adapters independently testable.
5. Preserve existing Calendar Action ABI and generated-source rules.
6. Synchronize logical focus, actual NUI focus, and focused ViewAnnotation.
7. Surface empty, loading, validation, persistence, and recovery states instead of silently failing.

## 3. Non-goals

- Month navigation and view switching will not become public Tizen Actions.
- The Calendar UI will not invoke its own provider through Action RPC.
- Existing `Tv_Tizen.Action.Calendar_Search` will not be modified incompatibly.
- Generated Action/Entity source will not be edited manually.
- The design will not copy Google or Apple branding or proprietary assets.
- Full recurrence editing, account synchronization, and cloud calendar integration are outside this slice.

## 4. Information architecture and visual structure

### 4.1 Persistent command bar

Left to right:

1. Current period label
2. Previous-period button
3. Today button
4. Next-period button
5. Flexible spacer
6. Search button
7. Mode tabs: Month, Week, Day, Agenda

At 1920×1080, primary controls target 56–60 px height and at least a 60×60 px pointer hit surface. Responsive scaling retains the current `min(width / 1920, height / 1080)` model, with a 40 px minimum practical control height. The Today text remains visible when horizontal space contracts; spacing collapses before labels do.

Focus uses at least two non-color cues: strong outline plus background/elevation or scale. Functional icons receive accessible names; decorative icons stay out of the accessibility tree.

### 4.2 Month

- Seven weekday columns and 42 independent date hit surfaces.
- The complete selected month remains the primary discovery surface.
- The selected date's agenda stays in the right 32% pane.
- Previous/next moves one month while preserving the day number where valid and clamping to the target month's final day otherwise.

### 4.3 Week

- Seven columns with a shared time axis.
- Previous/next moves seven days.
- Directional focus moves among time/event slots without wrapping unexpectedly.
- Enter on an event opens detail; an empty slot may open create with the slot's date/time preselected.

### 4.4 Day

- One-day time axis with event cards.
- Previous/next moves one day.
- Event cards use stable event IDs for focus restoration and ViewAnnotation identity.

### 4.5 Agenda

- Chronological, date-grouped list for the active range.
- Previous/next moves one month.
- No-result state includes a focusable Add event recovery action.

### 4.6 Advanced search overlay

- Query text searches title, location, and note.
- Explicit start-inclusive and end-exclusive date range.
- Search field selectors allow Title, Location, and Notes, with all selected by default.
- Focus order: query, start date, end date, field selectors, Apply, results.
- Back unwinds result interaction, then filters, then returns to the originating view and control.
- Results use the same event detail/editor path as calendar views.

## 5. UI state and commands

Introduce explicit state rather than deriving behavior from view layout:

- `CalendarViewMode`: Month, Week, Day, Agenda
- visible anchor/period
- selected date
- focused region and stable focused element ID
- open surface: calendar, search, result, detail, editor, confirmation
- search draft, applied criteria, result IDs, loading/error state

Semantic commands include:

- `ShowPreviousPeriod`
- `ShowNextPeriod`
- `ActivateToday`
- `ChangeViewMode(mode)`
- `OpenSearch`
- `UpdateSearchDraft(criteria)`
- `ApplySearch`
- `SelectSearchResult(eventId)`
- existing date/event/editor/reminder commands

Pointer and remote activation must dispatch the same commands through the reducer/application service path.

## 6. State invariants

1. `VisibleMonth` is always the first day of its month in Month mode.
2. `SelectedDate` always lies in the displayed period after period navigation.
3. Month movement preserves the selected day where possible and clamps at month end.
4. Period movement clears stale agenda indexes and focused event IDs.
5. View-mode changes preserve the selected date as the anchor.
6. Search application is immutable: draft criteria become an applied snapshot.
7. Back returns to the stable element that opened the current surface.
8. Re-rendered NUI focus and logical focus identify the same semantic element.

## 7. Domain search service

Add a Tizen-free criteria model and repository/service query:

- keyword
- start-inclusive
- end-exclusive
- selected fields: title, location, note
- optional result limit

The UI and Calendar Action provider use the same domain service. The UI does not call Action RPC. Search ordering remains start time then stable event ID.

Invalid ranges return a typed validation failure. Persistence or restore failures produce a visible recoverable state rather than a silent return.

## 8. Action architecture

### 8.1 Existing Actions

Keep `Tv_Tizen.Action.Calendar_Search` unchanged. It continues accepting `Tizen.Entity.Query` for keyword-oriented compatibility.

### 8.2 New typed period search

Add, subject to authoritative schema naming verification:

- Entity: `Tizen.Entity.CalendarSearchQuery`
  - `Keyword`
  - `StartDate`
  - `EndDate`
  - `SearchTitle`
  - `SearchLocation`
  - `SearchNote`
  - `Number`
- Action: `Tv_Tizen.Action.Calendar_SearchInPeriod`
  - input: `Tizen.Entity.CalendarSearchQuery`
  - output: Status plus Calendar Entity array

This is additive. Existing positional method IDs remain unchanged, and the new method is appended through the authoritative `action.seq` workflow. Bindings are regenerated with `actionc -a Tizen.Action.Calendar`; generated files are never patched manually. The provider manifest advertises the new Action only after the implementation returns a successful typed result.

## 9. ViewAnnotation contract

- Header controls, date cells, and empty slots are not Calendar Entity annotations.
- Actual rendered event cards in Month agenda, Week, Day, Agenda, and search results are annotated.
- `Annotation.EntityType` remains `Tizen.Entity.Calendar`.
- `Annotation.EntityId` remains the stable event ID.
- `Annotation.EntityInfo` remains the generated Calendar Entity `ToJson()` snapshot.
- Published views are replaced when period, view mode, search result, overlay, or app activation changes.
- Annotation presence is represented by the `Annotation` object itself.
- Focused annotation must match actual NUI focus.
- Measured `ScreenBounds` and window-relative `WindowBounds` replace synthetic zero bounds.

The generated Calendar snapshot currently includes note and location. This design preserves the canonical `ToJson()` requirement but records disclosure as an explicit product policy: annotations are published only while the corresponding card is rendered in the foreground. Redaction would require a separate Entity contract rather than manually mutating canonical generated JSON.

## 10. Focus and accessibility

- Initial focus: selected date in Month; selected time/event in Week/Day; first event or Add event in Agenda.
- Header Left/Right is deterministic and does not create unexpected wraparound.
- Header Down returns to the stable content anchor.
- Content Up at the boundary enters the nearest meaningful header control.
- Pointer activation also establishes actual NUI focus.
- Overlay focus is trapped and restored by stable semantic ID.
- Date cells expose localized full date, weekday, today/selected/out-of-month state, event count, and hidden count.
- Event cards expose title, time, and location as one button-like semantic unit.
- Validation errors are visible, announced, and focus the first invalid field.
- Save/delete/restore failures expose retry or close actions.

## 11. Component boundaries

- `CalendarUiState` / reducer: pure navigation and mode/search state transitions.
- View-specific presentation models: pure Month/Week/Day/Agenda layout data.
- NUI view builders: render presentation models and dispatch semantic commands.
- Domain search service: criteria validation and event filtering.
- Application composition root: command orchestration, persistence, overlay lifecycle, and focus restoration.
- Calendar Action provider: generated DTO adaptation only.
- View Action provider: rendered/focused event-card snapshot publication only.

No screen model depends on generated Action DTOs.

## 12. Implementation slices

1. P0 command bar, month navigation state, focus regions, and pointer/remote parity.
2. Shared view mode state plus Month/Week/Day/Agenda presentation models and renderers.
3. Advanced search domain criteria, overlay, results, and recovery states.
4. Additive `CalendarSearchQuery` and `Calendar_SearchInPeriod` schema/provider integration.
5. Accurate rendered-card annotations, actual focus synchronization, lifecycle clearing, and bounds.
6. Accessibility semantics, failure states, visual polish, and device acceptance.

Each slice follows RED–GREEN–REFACTOR and is kept buildable.

## 13. Verification

### Host tests

- previous/next period in every mode
- Jan/Dec year boundary and leap-year/month-end clamping
- mode changes preserve selected date
- deterministic header/content focus transitions and Back restoration
- pointer Down/Up-inside, drag-out cancellation, and no duplicate activation
- search keyword/field/date-range matching, ordering, limits, and invalid ranges
- empty/error/loading states
- ViewAnnotation publication set, stale clearing, focused ID, and generated `ToJson()` snapshot
- existing Calendar/Schedule/CRUD/persistence regression

### Generation/build checks

- baseline build before schema work
- whole-category `actionc -a Tizen.Action.Calendar`
- generated-output reproducibility check
- project builds with zero new warnings/errors
- `git diff --check`

### Common Emulator E2E

- pointer and D-pad command bar traversal
- Month/Week/Day/Agenda switching
- previous/Today/next behavior in every mode
- advanced search with date range and field selectors
- Action discovery and execution for old Search and new SearchInPeriod
- event CRUD and restart persistence
- rendered/focused ViewAnnotation and A2UI `ToPresentation`
- screenshot evidence only after functional paths pass

TV-profile/product verification remains a separate later gate.

## 14. Acceptance criteria

- Previous, Today, and Next controls are visible, independently focusable, and work by pointer and remote.
- All four view modes render and navigate according to their defined period.
- Advanced search filters title/location/note by explicit date range and has a recoverable empty/error flow.
- Existing Calendar Action behavior and method IDs remain compatible.
- The new typed period-search Action is discoverable and returns the same results as the app-owned domain search service.
- Actual NUI focus, visual focus, and focused ViewAnnotation agree.
- Only rendered event cards are visible annotations; stale annotations are cleared.
- Host regressions, generation checks, builds, and Common Emulator E2E pass with captured evidence.

## 15. Risks and mitigations

- Scope size: deliver in buildable vertical slices; do not combine all view renderers into one large class.
- NUI focus loss on root rebuild: restore by stable semantic ID initially; consider incremental view retention only if device evidence shows rebuild is unreliable.
- Schema compatibility: add a new Entity/Action rather than changing existing Search input.
- Annotation privacy: publish only foreground rendered cards; do not hand-redact generated canonical Entity JSON.
- Dense command bar at smaller resolutions: collapse spacing first and use tested minimum control sizes; keep labels accessible even if visually compacted.
