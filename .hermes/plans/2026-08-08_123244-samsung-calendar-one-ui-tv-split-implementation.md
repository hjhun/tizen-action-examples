# Samsung Calendar-inspired One UI TV Split Implementation Plan

> **For Hermes:** Execute this plan task-by-task with test-driven development. Keep all changes local; do not commit or push. Preserve untracked `.dev/`, `.hermes/`, `.superpowers/`, and `Calendar/` content.

**Goal:** Replace the current concatenated-text Calendar screen with the approved B design: a responsive Samsung Calendar-inspired light 68:32 month-grid/agenda split that is fully navigable by TV remote and verified on `emulator-26101`.

**Architecture:** Keep `CalendarApplication` as the composition root and use the existing shared `CalendarEventRepository` for both UI and provider. Extend pure, host-testable Calendar UI state/presentation models first, then render each date cell and agenda item as independent NUI views. Keep generated provider output and resolver contracts unchanged.

**Tech Stack:** C#/.NET 8, Tizen NUI/TizenFX, existing Calendar Domain and ActionProvider projects, executable host tests, Tizen CLI/SDB, Public Tizen 10.1 Common Emulator, `winfo` screenshot capture.

**Design source:** `.hermes/plans/2026-08-08-samsung-calendar-one-ui-tv-split-design.md`

---

## Constraints and non-goals

- Do not edit `Calendar/src/Calendar.ActionProvider/Generated/CalendarActionProvider.cs`.
- Do not change `CalendarService.GetEventByIds`, `ResolveByIds`, framework C API, resolver schema, or provider registration contract.
- Do not make UI queries through the app's own Action provider.
- Do not implement Search, Month/Week/Day/Schedule switching, event editor, persistence, Samsung Account integration, or dark-theme behavior in this slice.
- Menu and Search remain visible but disabled/unfocusable; Today is enabled.
- Add remains visible but disabled until event creation exists.
- Do not claim visual success from source/build alone; fresh emulator capture is mandatory.
- Do not commit or push. Preserve the user's local-patch workflow.

## Task 1: Extend pure UI state for split-view focus regions

**Objective:** Model month, agenda, and Today focus transitions independently of NUI.

**Files:**
- Modify: `Calendar/src/Calendar.App/CalendarUiState.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**Step 1 — RED:** Add tests covering:

- initial focus region is `MonthGrid`;
- Enter on a date with events moves to `Agenda` and focuses event index 0;
- Enter on an empty date moves to `AgendaEmptyState`;
- agenda Up/Down is bounded by event count;
- Left or Back from agenda restores the exact selected date in `MonthGrid`;
- Up from the top month row focuses `Today`;
- Down from Today restores the date cell;
- activating Today selects the supplied current date and updates the visible month;
- root Back requests exit only from `MonthGrid`.

Use a small enum such as:

```csharp
public enum CalendarFocusRegion
{
    MonthGrid,
    AgendaEvents,
    AgendaEmptyState,
    Today,
}
```

Add `FocusedAgendaIndex` as nullable or use `-1` only if tests make that invariant explicit.

**Step 2 — verify RED:**

```bash
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
```

Expected: compile/assertion failure because focus-region transitions do not exist.

**Step 3 — GREEN:** Implement minimal immutable transitions in `CalendarUiState`:

- `EnterAgenda(int eventCount)`
- `MoveAgenda(int delta, int eventCount)`
- `ReturnToMonth()`
- `FocusTodayControl()`
- `ActivateToday(DateOnly today)`
- focused-region-aware `HandleBack()`

Keep `MoveDays` deterministic and reset agenda focus when the selected date changes.

**Step 4 — verify GREEN:** Re-run `Calendar.App.Tests`; expect `Calendar.App.Tests: PASS`.

**Step 5 — checkpoint:** Run `git diff --check`; do not commit.

## Task 2: Add host-testable presentation models

**Objective:** Define all 42 cell and agenda-card content/state without NUI dependencies.

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarMonthPresentation.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**Step 1 — RED:** Add tests for August 2026 asserting:

- exactly 42 `CalendarDateCellPresentation` items;
- first date `2026-07-26` and final date from the existing 42-cell contract;
- out-of-month, in-month, today, selected, and focused flags are independent;
- Sunday is identified independently of color;
- zero, one, two, and more-than-two event cases;
- at most two event chips per date;
- overflow marker is `+N` with the exact hidden event count;
- agenda all-day events precede timed events, then start time and stable ID;
- empty day yields an explicit focusable `No events` presentation;
- event title/location are preserved as data while truncation remains a rendering concern.

Include source files directly in the host-compatible test project if required by the existing pattern.

**Step 2 — verify RED:** Run `Calendar.App.Tests`; expect missing presentation types.

**Step 3 — GREEN:** Implement pure records and factory methods, for example:

```csharp
public sealed record CalendarEventChipPresentation(
    string EventId,
    string Title,
    CalendarEventColorRole ColorRole);

public sealed record CalendarDateCellPresentation(
    DateOnly Date,
    bool IsInVisibleMonth,
    bool IsSunday,
    bool IsToday,
    bool IsSelected,
    bool IsFocused,
    IReadOnlyList<CalendarEventChipPresentation> EventChips,
    int OverflowCount);
```

Use repository overlap queries. Do not duplicate resolver logic or map to generated `TizenEntityCalendar` DTOs.

**Step 4 — verify GREEN:** Re-run `Calendar.App.Tests` and `Calendar.Domain.Tests`; expect both PASS.

**Step 5 — checkpoint:** Run `git diff --check`; do not commit.

## Task 3: Define Samsung-inspired light design tokens

**Objective:** Centralize the approved One UI-inspired colors, typography scales, spacing, radii, and focus treatment.

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarTheme.cs`
- Modify: `Calendar/src/Calendar.App/Calendar.App.csproj` only if explicit inclusion is required

**Step 1 — RED:** Add structural assertions to `Calendar.App.Tests` for the pure token descriptor, covering:

- root/secondary/cell surfaces;
- primary/secondary/disabled text;
- Sunday accent;
- today pill;
- selected cell tint;
- focus outline width and scale;
- event green/blue/orange/purple roles;
- reference safe insets and 68:32 split ratio.

Keep the test target free of NUI native types by defining a pure token descriptor and adapting it to `Color` in the NUI renderer.

**Step 2 — verify RED:** Run `Calendar.App.Tests`; expect missing token type.

**Step 3 — GREEN:** Implement named immutable light tokens. No raw color literals should remain scattered through view classes except inside the theme adapter.

**Step 4 — verify GREEN:** Run `Calendar.App.Tests`; expect PASS.

## Task 4: Render each date as an independent NUI view

**Objective:** Replace the multiline text grid with 42 independently styled NUI date cells.

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarDateCellView.cs`
- Rewrite: `Calendar/src/Calendar.App/CalendarMonthView.cs`

**Step 1 — compile checkpoint before edit:**

```bash
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
```

Expected: PASS on the pre-task state.

**Step 2 — implement minimal NUI cell:** Build a cell containing:

- date number label;
- today pill background;
- up to two event chip rows;
- `+N` overflow label;
- independent surface/background;
- focused outline/elevation/scale;
- selected tint;
- reduced out-of-month contrast.

Every child must use bounded width/height and ellipsis/clipping policies so one long event cannot grow the cell.

**Step 3 — compose month pane:** Build:

- header row with disabled Menu/Search affordances, centered month/year, enabled Today;
- weekday row;
- six rows × seven columns of `CalendarDateCellView`;
- scalable safe-area dimensions from the active window size.

Do not concatenate the grid into a single `TextLabel`.

**Step 4 — verify compile:** Re-run the Calendar app build. Expected: zero compile errors.

**Step 5 — static structure verification:** Add an OS-safe temporary verifier under `/tmp` that asserts the source instantiates 42 cell presentations/views through row/column composition and no longer calls the old `BuildGrid` multiline-text renderer. Run it, report the output, and delete it.

## Task 5: Render the persistent agenda pane

**Objective:** Implement the right-side 32% selected-date agenda with independent cards and empty state.

**Files:**
- Create: `Calendar/src/Calendar.App/SelectedDayAgendaView.cs`
- Modify: `Calendar/src/Calendar.App/CalendarMonthView.cs`

**Step 1 — implement agenda header:** Show large day number, weekday, full date, and event count.

**Step 2 — implement event cards:** Render all-day cards first, then chronological timed cards with:

- time or `All day`;
- title;
- optional location;
- calendar-color accent;
- focused outline/elevation without replacing event color.

**Step 3 — implement empty state:** Render a focusable `No events` card. Show Add as disabled and unfocusable.

**Step 4 — implement overflow:** Place agenda items in a vertically bounded/scrollable container; moving agenda focus keeps the focused card visible without changing date selection.

**Step 5 — verify compile:** Build `Calendar.App` and run `Calendar.App.Tests`; expect both PASS.

## Task 6: Wire D-pad routing and focus restoration

**Objective:** Make NUI input behavior match the approved state contract.

**Files:**
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify: `Calendar/src/Calendar.App/CalendarUiState.cs` only if a tested transition gap is found
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**Step 1 — RED:** Add any missing logical command tests for:

- Left/Right ±1 date and Up/Down ±7 dates in month mode;
- Up from top row to Today;
- Today activation;
- Enter to agenda event/empty state;
- bounded agenda Up/Down;
- Left/Back to exact selected date;
- root-only exit.

**Step 2 — verify RED:** Run `Calendar.App.Tests`; confirm the new expectation fails before routing changes.

**Step 3 — GREEN:** Route available Tizen key names for Left, Right, Up, Down, Return/Enter/Select, Escape/XF86Back. Re-render only after accepted transitions. Keep disabled Menu/Search/Add controls out of the focus path.

**Step 4 — verify GREEN:** Run `Calendar.App.Tests` and build `Calendar.App`; expect PASS.

## Task 7: Run focused quality gates

**Objective:** Establish fresh passing evidence for every changed behavior before packaging.

**Files:** No production edits unless failures reveal defects.

Run from `/home/hjhun/samba/workspace/tizen-action-examples`:

```bash
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
dotnet build Calendar/tests/Calendar.ActionProvider.Tests/Calendar.ActionProvider.Tests.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
git diff --check
```

Expected:

- `Calendar.Domain.Tests: PASS`
- `Calendar.App.Tests: PASS`
- provider test project build succeeds
- Calendar app build succeeds
- `git diff --check` exits 0

Create one OS-safe temporary ad-hoc verifier with Python `tempfile`, run it against the month-grid/agenda presentation invariants, print a PASS marker, and delete it. Report it as ad-hoc evidence, not canonical-suite coverage.

## Task 8: Build, package, and inspect the TPK

**Objective:** Produce a complete emulator-signed package containing app, domain, and provider assemblies.

**Files:**
- Replace local artifact: `Calendar/org.tizen.actionexamples.calendar-0.1.0.tpk`

**Steps:**

1. Run `tizen build-cs -C Debug` from `Calendar/src/Calendar.App`.
2. Create an OS-safe temporary staging directory.
3. Copy the complete `bin/Debug/net8.0` output and `tizen-manifest.xml` into staging.
4. Run generic packaging without `--profile`:

```bash
$HOME/tizen-studio/tools/ide/bin/tizen package -t tpk -o <temporary-output> -- <staging>
```

5. Copy the resulting signed archive to `Calendar/org.tizen.actionexamples.calendar-0.1.0.tpk` even if the generic packager emits an unexpected source filename extension.
6. Verify archive entries include:

```text
author-signature.xml
signature1.xml
tizen-manifest.xml
lib/Calendar.App.dll
lib/Calendar.Domain.dll
lib/Calendar.ActionProvider.dll
```

7. Compute and record the fresh SHA-256.
8. Delete temporary staging/output directories.

## Task 9: Install and exercise on the Common Emulator

**Objective:** Validate runtime behavior and remote navigation on `emulator-26101`.

**Steps:**

1. Confirm `sdb devices` lists `emulator-26101`.
2. Install the new TPK with Tizen CLI.
3. Launch `org.tizen.actionexamples.calendar`.
4. Confirm the process survives and inspect runtime logs for crash/fatal/NUI layout errors.
5. Capture the initial month split view using:

```bash
winfo -dump topvwins /tmp/hermes-calendar-one-ui-split
```

6. Pull the main Calendar window image.
7. Inject or manually exercise deterministic key transitions where supported:

- move one day left/right;
- move one week up/down;
- cross a month boundary;
- enter an event-bearing agenda;
- return with Back;
- enter an empty date;
- return with Back;
- activate Today.

8. Capture at least:

- initial split view;
- focused date with event chips;
- agenda-event focus;
- empty-state focus.

9. Confirm Calendar resolver provider registration remains present after reinstall.

## Task 10: Visual acceptance against candidate B

**Objective:** Decide success from actual emulator pixels, not source intent.

**Reference mockup:**

```text
.superpowers/brainstorm/3348167-1786158879/content/samsung-calendar-tv-layouts.html
Candidate B — One UI TV Split
```

Inspect the emulator captures for:

- 68:32 month/agenda hierarchy;
- bright Samsung-inspired neutral surface;
- centered month/year header;
- seven weekday headers and 42 independent cells;
- red Sunday role;
- dark today pill;
- pale selected cell;
- high-contrast outline + shadow + scale focus;
- up to two pastel event chips and `+N` overflow;
- large agenda date and readable event cards;
- no clipping, overlap, oversized typography, or overscan violations.

If the first capture differs materially, revise only theme/layout/view code, rebuild, repackage, reinstall, and recapture until the criteria pass. Do not weaken acceptance criteria to match a failed render.

## Task 11: Update durable records

**Objective:** Preserve verified evidence and accurate remaining scope.

**Files:**
- Modify: `.dev/DASHBOARD.md`
- Modify: `.dev/progress/developer.md`
- Update: `/home/hjhun/samba/workspace/hermes-workspace/2026/2026-08/calendar-provider-development-status-2026-08-08.md`
- Create: `/home/hjhun/samba/workspace/hermes-workspace/2026/2026-08/calendar-one-ui-tv-split-emulator-2026-08-08.png`

Record:

- exact focused verification commands/results;
- fresh TPK SHA-256;
- emulator serial/profile/platform;
- install/launch/process/log result;
- provider registration result;
- screenshot path and visual assessment;
- explicit deferred scope.

Do not commit or push unless the user separately requests it.

---

## Final completion criteria

The work is complete only when:

1. source renders actual independent NUI date/event views, not multiline text;
2. pure state/presentation tests pass;
3. provider build remains intact and generated code is untouched;
4. package/install/launch succeeds on `emulator-26101`;
5. remote focus transitions are exercised;
6. fresh `winfo` captures visibly match candidate B;
7. durable records contain the actual evidence and remaining scope;
8. no commit or push has been made.
