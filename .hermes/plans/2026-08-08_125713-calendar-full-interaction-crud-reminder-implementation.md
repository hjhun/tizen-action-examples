# Calendar Full Interaction, CRUD, and Reminder Implementation Plan

> **For Hermes:** Execute in strict RED→GREEN vertical slices. Do not commit or push. Preserve generated-code boundaries and all existing untracked workspace content.

**Goal:** Turn the Samsung Calendar-inspired B UI into a persistent, touch/remote-complete Calendar with event CRUD, event-linked alarms, independent reminders, and working Calendar/Schedule Action providers.

**Architecture:** UI touch and remote events dispatch typed commands into shared UI/application state. `CalendarCommandService` is the sole mutation boundary across UI and providers, coordinating thread-safe repositories, versioned JSON persistence, and an injected reminder scheduler. Existing schemas are consumed as-is; generated bindings are regenerated through `actionc` and never edited manually.

**Tech Stack:** C#/.NET 8, Tizen NUI/TizenFX 13, System.Text.Json, Tizen Alarm/Notification APIs, TIDL/actionc-generated C# providers, executable host tests, Tizen CLI/SDB/Common Emulator.

**Design:** `.hermes/plans/2026-08-08-calendar-crud-touch-reminder-design.md`

---

## Task 1: Establish fresh baseline and development records

**Files:**
- Modify: `.dev/DASHBOARD.md`
- Modify: `.dev/progress/developer.md`

**Steps:**

1. Record `git status --short`; preserve all existing content.
2. Run:

```bash
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
dotnet build Calendar/tests/Calendar.ActionProvider.Tests/Calendar.ActionProvider.Tests.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
git diff --check
```

3. Record actual baseline output.
4. Mark Developer `in_progress` without changing unrelated workflow statuses.

## Task 2: Add typed UI commands and touch equivalence

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarUiCommand.cs`
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify: `Calendar/src/Calendar.App/CalendarDateCellView.cs`
- Modify: `Calendar/src/Calendar.App/SelectedDayAgendaView.cs`
- Modify: `Calendar/src/Calendar.App/CalendarMonthView.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**RED:** Test that touch-date, remote-date, touch-event, remote-event, Today, Add, Edit, Delete, and Back map to the same semantic command/state transitions. Verify a Down without an Up-inside never activates.

**GREEN:** Add immutable command records/enums and one dispatcher. Attach NUI `TouchEvent` handlers that activate only on Up-inside after matching Down. Views publish commands; they do not access repositories.

**Verify:** `Calendar.App.Tests` PASS and `Calendar.App` build PASS.

## Task 3: Make the event repository mutable and thread-safe

**Files:**
- Modify: `Calendar/src/Calendar.Domain/CalendarEventRepository.cs`
- Modify: `Calendar/tests/Calendar.Domain.Tests/Program.cs`

**RED:** Cover Add success/duplicate, Update success/not-found, Delete success/not-found, case-insensitive Search, ordered Snapshot, and parallel read/mutation safety.

**GREEN:** Protect the dictionary with a lock, keep immutable result arrays, and preserve existing `ResolveByIds` and overlap ordering.

**Verify:** Domain tests PASS; existing provider resolution tests remain green.

## Task 4: Add reminder domain and repository

**Files:**
- Create: `Calendar/src/Calendar.Domain/CalendarReminder.cs`
- Create: `Calendar/src/Calendar.Domain/CalendarReminderRepository.cs`
- Modify: `Calendar/tests/Calendar.Domain.Tests/Program.cs`

**RED:** Cover validation, stable IDs, create/update/delete/search/complete/reopen, linked event IDs, offset values 10/30/60/1440, alarm metadata, ordering, and parallel snapshots.

**GREEN:** Implement thread-safe repository and immutable domain records. Do not reference Tizen APIs from Domain.

## Task 5: Add versioned atomic JSON persistence

**Files:**
- Create: `Calendar/src/Calendar.Persistence/Calendar.Persistence.csproj`
- Create: `Calendar/src/Calendar.Persistence/CalendarJsonStore.cs`
- Create: `Calendar/src/Calendar.Persistence/CalendarStoreDocument.cs`
- Create: `Calendar/tests/Calendar.Persistence.Tests/Calendar.Persistence.Tests.csproj`
- Create: `Calendar/tests/Calendar.Persistence.Tests/Program.cs`
- Modify: `Calendar/src/Calendar.App/Calendar.App.csproj`
- Modify: `Calendar/src/Calendar.ActionProvider/Calendar.ActionProvider.csproj`

**RED:** Cover event/reminder round trip, empty store, schema version rejection/migration boundary, temp-write atomicity, failed replace preserving old data, and corrupt-file backup/recovery.

**GREEN:** Implement `System.Text.Json` store with injected file path. Device path comes from `Application.Current.DirectoryInfo.Data`; host tests use a unique OS tempfile directory. Seed data only when no store exists.

## Task 6: Add transactional command service

**Files:**
- Create: `Calendar/src/Calendar.Domain/CalendarCommandService.cs`
- Create: `Calendar/src/Calendar.Domain/IReminderScheduler.cs`
- Modify: Domain and persistence test programs or create `Calendar.Application.Tests`

**RED:** Cover event create/update/delete, linked reminder reconciliation, independent reminder CRUD/complete, persistence after success, rollback after persistence failure, and explicit alarm-unavailable result.

**GREEN:** Implement the sole mutation boundary. Repository rollback uses pre-operation snapshots. Alarm failure does not discard saved data; persistence failure does.

## Task 7: Implement event detail/editor/delete UI state

**Files:**
- Create: `Calendar/src/Calendar.App/CalendarEditorState.cs`
- Create: `Calendar/src/Calendar.App/CalendarEventEditorView.cs`
- Create: `Calendar/src/Calendar.App/CalendarEventDetailView.cs`
- Create: `Calendar/src/Calendar.App/CalendarConfirmationDialog.cs`
- Modify: `Calendar/src/Calendar.App/CalendarUiState.cs`
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**RED:** Cover add prefill, edit population, title/range validation, multiple reminder presets, Save enablement, Cancel preservation semantics, Delete confirmation defaulting to Cancel, and Back hierarchy.

**GREEN:** Implement Samsung-inspired right-side editor/detail overlays. Text fields use bounded NUI controls and explicit focus order. Add/Edit/Delete dispatch commands to `CalendarCommandService`.

## Task 8: Implement Calendar mutation/search/presentation Actions

**Files:**
- Modify: `Calendar/src/Calendar.ActionProvider/CalendarService.cs`
- Modify: `Calendar/src/Calendar.ActionProvider/CalendarActionProviderHost.cs`
- Modify: `Calendar/tests/Calendar.ActionProvider.Tests/Program.cs`

**RED:** Add tests for valid and invalid AddEvent, UpdateEvent, RemoveEvent, Search, and ToPresentation while retaining GetEventByIds ordering/unresolved behavior.

**GREEN:** Map generated entities to domain with strict ID/title/ISO date/range validation. Call `CalendarCommandService`; do not mutate repositories directly.

## Task 9: Implement Tizen alarm/notification adapter

**Files:**
- Create: `Calendar/src/Calendar.App/TizenReminderScheduler.cs`
- Modify: `Calendar/src/Calendar.App/tizen-manifest.xml`
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Create/modify host tests around a fake scheduler

**RED:** With a fake scheduler, cover future scheduling, no past/completed scheduling, reschedule after event update, cancellation after delete/complete, and startup reconciliation.

**GREEN:** Use `AlarmManager.CreateAlarm(DateTime, Notification)`, persist `AlarmId`, call only the specific `Alarm.Cancel()`, and add `alarm.set`, `alarm.get`, and `notification` privileges. Never call `CancelAll`.

## Task 10: Generate and implement Schedule Action provider

**Files:**
- Create: `Calendar/src/Calendar.ScheduleActionProvider/Calendar.ScheduleActionProvider.csproj`
- Generate: `Calendar/src/Calendar.ScheduleActionProvider/Generated/ScheduleActionProvider.cs`
- Create: `Calendar/src/Calendar.ScheduleActionProvider/ScheduleService.cs`
- Create: `Calendar/src/Calendar.ScheduleActionProvider/ScheduleActionProviderHost.cs`
- Create: `Calendar/tests/Calendar.ScheduleActionProvider.Tests/Calendar.ScheduleActionProvider.Tests.csproj`
- Create: `Calendar/tests/Calendar.ScheduleActionProvider.Tests/Program.cs`
- Modify: `Calendar/src/Calendar.App/Calendar.App.csproj`
- Modify: `Calendar/src/Calendar.App/tizen-manifest.xml`
- Modify: `Calendar/src/Calendar.App/tizen_dotnet_project.yaml`

**RED:** Tests for CreateReminder, UpdateReminder, DeleteReminder, SearchReminder, CompleteReminder, validation, missing ID, and deterministic result ordering.

**GREEN:** Run `actionc` against the existing Schedule schemas/entities. Implement generated `ServiceBase` through `CalendarCommandService`. Register all implemented Schedule Actions in manifest metadata. Verify generated output can be reproduced byte-for-byte from `actionc`.

## Task 11: Implement Reminder list/editor UI

**Files:**
- Create: `Calendar/src/Calendar.App/ReminderListView.cs`
- Create: `Calendar/src/Calendar.App/ReminderEditorView.cs`
- Modify: `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify: `Calendar/src/Calendar.App/CalendarUiState.cs`
- Modify: `Calendar/tests/Calendar.App.Tests/Program.cs`

**RED:** Cover incomplete-before-completed ordering, create/edit/delete, complete/reopen, linked-event display, touch/remote command equivalence, focus restoration, and Back hierarchy.

**GREEN:** Add `Calendar`/`Reminders` menu navigation and Samsung-inspired reminder list/editor using the same interaction and confirmation patterns as events.

## Task 12: Full host/build verification

Run all focused tests and builds:

```bash
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
dotnet run --project Calendar/tests/Calendar.Persistence.Tests/Calendar.Persistence.Tests.csproj
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
dotnet run --project Calendar/tests/Calendar.ActionProvider.Tests/Calendar.ActionProvider.Tests.csproj
dotnet run --project Calendar/tests/Calendar.ScheduleActionProvider.Tests/Calendar.ScheduleActionProvider.Tests.csproj
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
git diff --check
```

Create an OS-safe temporary ad-hoc verifier with Python `tempfile` for generated-code reproduction, provider metadata coverage, manifest privileges, and package input completeness. Run it, report it as ad-hoc evidence, and delete it.

## Task 13: Package, install, and device E2E

1. Run `tizen build-cs -C Debug`.
2. Stage complete `bin/Debug/net8.0` output plus manifest.
3. Package without custom profile using generic emulator signing.
4. Inspect signatures, manifest, app/domain/persistence/Calendar provider/Schedule provider assemblies.
5. Install on `emulator-26101` and launch.
6. Verify process/log survival.
7. Verify Calendar and Schedule provider metadata in Action DB.
8. Exercise actual Calendar Add/Update/Search/GetByIds/Remove requests where the public invocation path permits.
9. Exercise Schedule Create/Update/Search/Complete/Delete requests.
10. Create an event and independent reminder through UI; restart app and verify persistence.
11. Verify alarms are scheduled and specific cancellation/reschedule behavior works. If Common Emulator lacks notification delivery, report scheduling evidence separately from notification-display evidence.
12. Exercise pointer/touch where emulator input supports it and D-pad equivalents for every operation.

## Task 14: Visual acceptance and durable records

Capture fresh `winfo` images for:

- root month/agenda;
- event detail;
- event editor with reminder presets;
- delete confirmation;
- reminder list;
- reminder editor;
- validation/error state.

Compare root hierarchy against candidate B. Confirm no clipping, dead controls, missing focus, or misleading disabled actions.

Update:

- `.dev/DASHBOARD.md`
- `.dev/progress/developer.md`
- `.dev/DEVELOPTMENT.md`
- `/home/hjhun/samba/workspace/hermes-workspace/2026/2026-08/calendar-provider-development-status-2026-08-08.md`
- durable screenshots under `/home/hjhun/samba/workspace/hermes-workspace/2026/2026-08/`

Record actual TPK SHA-256, emulator evidence, Action registration/invocation evidence, alarm scheduling/display distinction, known limitations, and remaining deferred scope. Do not commit or push.

---

## Completion criteria

Complete only when event CRUD, independent Reminder CRUD/complete, linked alarm presets, persistence across restart, touch/remote parity, Calendar/Schedule provider registration, focused Action E2E, package/install/launch, and fresh emulator screenshot acceptance have all been exercised with real evidence. Partial layers must be reported separately and never rolled up into an unqualified PASS.
