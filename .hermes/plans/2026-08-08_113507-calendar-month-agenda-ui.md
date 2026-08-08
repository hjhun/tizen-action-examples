# Calendar Month Grid and Agenda UI Implementation Plan

> **For Hermes:** Execute this plan in small TDD vertical slices. Preserve the generated-code boundary and the Action provider/domain split.

**Goal:** Replace the single clipped Calendar text screen with a TV remote-first month grid and selected-day agenda that runs on the Common Emulator.

**Architecture:** Keep `CalendarApplication` as a composition root. `CalendarEventRepository` remains the one source of truth shared by NUI and `CalendarActionProviderHost`; UI reads domain `CalendarEvent` objects directly and never calls the generated provider. Add only a period-query API and a small UI state/controller model (`VisibleMonth`, `SelectedDate`, `IsAgendaOpen`) so the month and agenda views are independently renderable.

**Tech Stack:** C#/.NET 8, Tizen NUI, existing `Calendar.Domain`, generated TIDL C# provider, Tizen CLI/SDB/winfo.

---

## Approved scope

- Always-complete 6×7 month grid, including de-emphasized leading/trailing dates.
- Selected-date agenda, chronological events, and a focusable empty state.
- Deterministic TV D-pad navigation: Left/Right ±1 day and Up/Down ±7 days.
- Enter opens the selected-day agenda; Back returns from agenda to the same grid cell; Back exits only from the root grid.
- Non-color-only focus: visible border, contrast, and scale/elevation treatment.
- This slice intentionally excludes week/day modes, event editing, persistent storage, themes, and the other Calendar Actions.

## Constraints

- Never hand-edit `Calendar/src/Calendar.ActionProvider/Generated/CalendarActionProvider.cs`; change TIDL input/template and regenerate only when needed.
- Preserve `CalendarService.GetEventByIds` and `ResolveByIds` behavior and C API/framework ABI.
- Do not claim UI success until package, install, launch, `winfo` screenshot, and log evidence are completed on `emulator-26101`.

## Task 1: Add a period query to the domain repository

**Objective:** Give the NUI layer an ordered, bounded date-range query without using provider DTOs.

**Files:**
- Modify: `Calendar/tests/Calendar.Domain.Tests/Program.cs`
- Modify: `Calendar/src/Calendar.Domain/CalendarEventRepository.cs`

**Step 1 — RED:** Add an executable test that creates events before, within, and after a `[startInclusive, endExclusive)` day/month period. Assert that only overlapping events are returned, in start-time then stable-ID order.

**Step 2 — verify RED:**

```bash
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
```

Expected: fail because the period query does not exist.

**Step 3 — GREEN:** Add `GetEventsOverlapping(DateTimeOffset startInclusive, DateTimeOffset endExclusive)` to `CalendarEventRepository`. Validate `endExclusive > startInclusive`; include events when `event.Start < endExclusive && event.End > startInclusive`; sort by `Start`, then `Id` ordinally.

**Step 4 — verify GREEN:** Re-run the domain executable test; expect `Calendar.Domain.Tests: PASS`.

## Task 2: Define and test pure Calendar UI state

**Objective:** Make date movement and agenda-mode transitions deterministic without depending on NUI rendering.

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarUiState.cs`
- Create: `Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj`
- Create: `Calendar/tests/Calendar.App.Tests/Program.cs`
- Modify: `Calendar/src/Calendar.App/Calendar.App.csproj` only if a project reference/testable visibility setup is needed.

**Step 1 — RED:** Write executable tests for:
- 42 dates in a Sunday-first 6×7 grid for August 2026;
- Left/Right and Up/Down movement across month boundaries;
- `VisibleMonth` follows `SelectedDate`;
- Enter opens agenda and Back closes agenda before root exit is requested.

**Step 2 — verify RED:**

```bash
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
```

Expected: compile/runtime failure because `CalendarUiState` is absent.

**Step 3 — GREEN:** Implement a small immutable or explicitly mutable `CalendarUiState` with `MoveDays`, `OpenAgenda`, `CloseAgenda`, and `BuildMonthCells` helpers. Use `DateOnly` for display dates; translate date boundaries to `DateTimeOffset` only at the repository query boundary.

**Step 4 — verify GREEN:** Run the UI-state executable test and expect its PASS marker.

## Task 3: Render the month grid and selected-day agenda

**Objective:** Replace the single full-window `TextLabel` with responsive NUI controls for the approved home flow.

**Files:**
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Create: `Calendar/src/Calendar.App/CalendarMonthView.cs`
- Create: `Calendar/src/Calendar.App/SelectedDayAgendaView.cs`

**Step 1 — RED:** Extend the UI-state test with a focused date containing two events, an empty date, and the expected agenda ordering/empty-state labels.

**Step 2 — verify RED:** Run `Calendar.App.Tests`; expected failure before view model/rendering support exists.

**Step 3 — GREEN:**
- Build a 16:9-safe root layout with header, weekday row, six date rows, and agenda panel/overlay.
- Render every date cell as a distinct control/label group; do not concatenate UI into one text string.
- Use date-cell states: in-month/out-of-month, today, selected, focused, event count, and agenda-open.
- Display at most two event title/time indicators per cell and a `+N` overflow marker.
- For the selected day, query the repository with its UTC day range and render time/title/location rows sorted by domain output.
- Render a focusable empty-state action label when no event overlaps the selected date.
- Keep demo data visibly labeled as demo data.

**Step 4 — verify GREEN:**

```bash
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
```

Expected: zero build errors and UI-state PASS.

## Task 4: Wire remote input and focus restoration

**Objective:** Make the approved keyboard/remote contract real rather than decorative.

**Files:**
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify: `Calendar/src/Calendar.App/CalendarUiState.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**Step 1 — RED:** Add tests translating logical directional commands to state changes, Enter to agenda open, and Back to agenda close/root-exit decision. Cover a month boundary and empty-date Enter.

**Step 2 — verify RED:** Run `Calendar.App.Tests`; expect assertion failure before the new command handling is implemented.

**Step 3 — GREEN:** Map available Tizen key names for Left, Right, Up, Down, Enter/Return, Escape/XF86Back. Re-render after every accepted state transition. On agenda close, restore focus styling to the same selected grid date. Permit `Exit()` only when root grid receives Back.

**Step 4 — verify GREEN:** Run the UI-state test suite and Calendar app build again.

## Task 5: Rebuild, package, deploy, and visually accept

**Objective:** Prove the NUI UI runs and looks like a Calendar on the Common Emulator.

**Files:**
- Modify: `.dev/DASHBOARD.md`
- Modify: `.dev/progress/developer.md`
- Create/modify: `.dev/DEVELOPTMENT.md`
- Create: `Calendar/tests/Calendar.App.Tests/README.md` only if key injection/manual verification needs a reproducible command reference.

**Steps:**
1. Run all focused domain, provider, and UI-state tests plus `git diff --check`.
2. Run `tizen build-cs -C Debug` from `Calendar/src/Calendar.App`.
3. Stage the complete `bin/Debug/net8.0` output and `tizen-manifest.xml`; run generic `tizen package -t tpk -o <out> -- <stage>` without `--profile` for default emulator-only signing.
4. Verify the TPK includes both signatures, manifest, `Calendar.App.dll`, `Calendar.Domain.dll`, and `Calendar.ActionProvider.dll`.
5. Install to `emulator-26101`, launch `org.tizen.actionexamples.calendar`, and check process/log survival.
6. Run `winfo -dump topvwins /tmp/hermes-calendar-month-ui` on the guest; pull the largest Calendar window image and inspect it. Acceptance requires readable month title, 7 weekday headers, 42 cells, selected/focus treatment, visible events, and a non-clipped agenda/empty state.
7. Store screenshot and final status in `hermes-workspace/2026/2026-08/` without publishing unless requested.

## Risks and deferred work

- The current public Tizen.NET package can compile the host but does not make all NUI behavior host-runnable; emulator capture is mandatory evidence.
- Exact NUI focus APIs may differ from standard UI frameworks. Preserve the pure state test as the deterministic behavior contract and keep NUI focus rendering implementation narrow.
- Week/day modes, editor workflows, persistence, themes, annotations, and the remaining Calendar Actions remain explicitly deferred rather than silently represented as complete.
