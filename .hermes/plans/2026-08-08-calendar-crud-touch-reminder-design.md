# Calendar Interaction, CRUD, Persistence, and Reminder Design

**Status:** Scope selected by user; awaiting written-spec confirmation before implementation  
**Extends:** `2026-08-08-samsung-calendar-one-ui-tv-split-design.md`  
**Target:** Tizen NUI Calendar app, pointer/touch + TV remote, Calendar and Schedule Actions

## 1. Product decision

The approved Samsung Calendar-inspired B screen becomes a complete interactive Calendar rather than a read-only demo. This scope includes all of the following:

1. pointer/touch interaction for dates, agenda cards, header controls, editor controls, and reminder controls;
2. event creation, update, deletion, search, and durable local persistence;
3. event-linked alarms with preset offsets of 10 minutes, 30 minutes, 1 hour, and 1 day;
4. independent reminders with create, update, delete, search, and complete operations;
5. implementation of the existing `Tizen.Action.Calendar` mutation Actions and `Tizen.Action.Schedule` reminder Actions;
6. one app-owned data model and command layer shared by UI and Action providers.

No generated source will be edited manually. Any new binding is produced by `actionc` from existing framework schemas.

## 2. Interaction model

### 2.1 Pointer/touch

Every enabled interactive view has one semantic command shared with remote input.

- Tap/click a date: select the date and update the agenda immediately.
- Double activation is not required; a second tap/click or Enter on the selected date enters the agenda.
- Tap/click an event chip or agenda card: select that event and open event details.
- Tap/click Add: open the event editor with the selected date prefilled.
- Tap/click Edit: open the same editor populated with the selected event.
- Tap/click Delete: open a destructive confirmation dialog; deletion never occurs on first touch.
- Tap/click Today: select today and update the visible month.
- Tap/click reminder rows/controls: select or edit the reminder.

NUI `TouchEvent` is handled as an Up-inside gesture after a matching Down, preventing drag-out or duplicate activation. Touch and remote dispatch the same typed `CalendarUiCommand`; views do not mutate repositories directly.

### 2.2 Remote

The existing deterministic month/agenda model remains. Editor and dialog focus paths are explicit:

- D-pad moves between fields and actions.
- Enter activates the focused field/action.
- Back closes keyboard, then popup/editor, then agenda, then exits only at root.
- Destructive confirmation defaults to Cancel.
- Disabled controls are never focusable or touch-active.

## 3. Screens and overlays

### 3.1 Month/agenda root

The approved 68:32 split remains unchanged. Add becomes enabled. Menu exposes `Calendar` and `Reminders`; Search becomes enabled after its query behavior is implemented.

### 3.2 Event detail

The agenda pane expands or overlays a detail surface containing title, start/end, location, note, linked reminders, Edit, and Delete. It retains the event stable ID for Action annotation and refresh.

### 3.3 Event editor

A right-side One UI-inspired sheet contains:

- Title, required;
- start date/time and end date/time, required and validated;
- All day toggle;
- location and note;
- reminder presets: None, 10 min, 30 min, 1 hour, 1 day; multiple presets are allowed;
- Cancel and Save;
- Delete only when editing an existing event.

Save remains disabled until title and time range are valid. Validation is shown inline and does not discard entered values.

### 3.4 Reminder list and editor

The Reminders surface lists incomplete reminders by due date, followed by completed reminders. The editor contains title, due date/time, note, optional linked Calendar event, Cancel, Save, Complete/Reopen, and Delete.

## 4. Domain and persistence

### 4.1 Event repository

`CalendarEventRepository` becomes a thread-safe mutable repository while preserving current ordered lookup and overlap-query behavior.

Operations:

- Add: stable ID must be unique.
- Update: stable ID must already exist.
- Delete: missing ID returns not-found rather than succeeding silently.
- Search: case-insensitive title/note/location matching with deterministic ordering.
- Snapshot: immutable ordered copy for persistence and provider responses.

A lock protects the mutable dictionary because UI callbacks and Action-provider calls can arrive on different threads.

### 4.2 Reminder model

A new app-owned `CalendarReminder` contains:

- stable ID;
- title;
- due date/time;
- note;
- completed state;
- optional `CalendarEventId`;
- optional `OffsetMinutes` for event-linked reminders;
- optional Tizen `AlarmId` persisted as scheduling metadata.

Event-linked reminders and independent reminders use the same repository. Event-linked reminder IDs are stable and derived/generated once, not recreated on every load.

### 4.3 Use-case/command service

`CalendarCommandService` is the only mutation boundary used by UI and both providers. It coordinates repositories, persistence, and alarms.

- Create event: add event, create selected linked reminders, schedule alarms, persist.
- Update event: update event, reconcile linked reminders, reschedule affected alarms, persist.
- Delete event: cancel and delete linked reminders, delete event, persist.
- Create/update/delete/complete reminder: mutate reminder, schedule/cancel as required, persist.

If alarm scheduling fails, the event/reminder remains saved but the result explicitly reports `AlarmUnavailable`; the UI displays a recoverable warning. Repository/persistence failure rolls back the in-memory mutation.

### 4.4 JSON store

`CalendarJsonStore` persists a versioned document under `Application.Current.DirectoryInfo.Data` on device. Host tests inject a temporary path.

The document contains `schemaVersion`, events, reminders, and alarm metadata. Writes use temp-file + atomic replace. Corrupt input is preserved as a `.corrupt-<timestamp>` backup and the app starts with a visible recovery state rather than silently overwriting it.

Seed demo data is used only when no store exists. Once a store exists, it is authoritative.

## 5. Alarm and notification integration

`IReminderScheduler` isolates platform APIs from domain tests.

The Tizen implementation uses:

- `Tizen.Applications.AlarmManager.CreateAlarm(DateTime, Notification)`;
- `Alarm.Cancel()` for update/delete/complete cleanup;
- `Alarm.AlarmId` persisted for reconciliation;
- privileges `http://tizen.org/privilege/alarm.set`, `alarm.get`, and `notification`.

At startup, scheduler reconciliation:

1. removes stale alarm IDs from records when alarms no longer exist;
2. reschedules future incomplete reminders without valid alarms;
3. does not schedule completed or past reminders;
4. never calls `CancelAll`, because the app must not affect unrelated alarms.

The notification title is the reminder/event title. Body contains the event time and optional location, excluding private note content.

## 6. Action-provider integration

### 6.1 Calendar category

Implement existing generated methods in `CalendarService`:

- `AddEvent`;
- `UpdateEvent`;
- `RemoveEvent`;
- `Search`;
- `ToPresentation`;
- preserve `GetEventByIds` exactly.

Entity validation rejects missing IDs/titles, invalid ISO timestamps, and non-positive ranges with structured failure reasons.

### 6.2 Schedule category

Generate a separate Schedule binding with `actionc` from existing `Tizen.Action.Schedule` and `Tizen.Entity.Reminder` schemas. Implement:

- `CreateReminder`;
- `UpdateReminder`;
- `DeleteReminder`;
- `SearchReminder`;
- `CompleteReminder`.

The provider uses the same `CalendarCommandService` and reminder repository as the UI. Existing framework schemas and `action.seq` are not changed.

### 6.3 Manifest

Register each implemented Calendar and Schedule Action as provider metadata. Add alarm/notification privileges. Provider registration is verified in the Action DB after TPK installation.

## 7. Error and confirmation behavior

- Invalid editor fields: inline validation, Save disabled.
- Duplicate event/reminder ID from Action: structured failure, no overwrite.
- Update/delete missing ID: not-found failure.
- Persistence failure: mutation rollback and visible error.
- Alarm permission/unavailability: data saved, warning shown, retry available.
- Delete event/reminder: confirmation required; Cancel is initial focus.
- Delete event with linked reminders: confirmation states that linked reminders will also be deleted.
- Reminder linked to missing event during load: retained as independent reminder and marked for review, not discarded.

## 8. TDD and verification boundaries

Host tests cover:

- mutable repository success/failure/order/thread-safe snapshots;
- command-service transaction and rollback behavior;
- JSON round-trip, migration version, atomic-write failure, corrupt recovery;
- linked reminder offset calculation and reconciliation;
- Calendar and Schedule provider mapping/validation;
- UI command routing equivalence between touch and remote;
- editor validation and destructive confirmation state.

Device verification covers:

- pointer/touch dispatch on Common Emulator where supported;
- D-pad and Enter/Back paths;
- real JSON persistence across process restart;
- alarm privilege and scheduling APIs;
- package, install, launch, log survival;
- Calendar and Schedule Action provider registration/discovery;
- fresh screenshots for root, editor, reminder list, and delete confirmation.

## 9. Implementation order

1. typed UI commands and touch routing;
2. mutable event repository and CRUD tests;
3. versioned JSON store and restart persistence;
4. event editor/detail/delete confirmation;
5. Calendar Action mutation/search/presentation methods;
6. reminder domain/repository/command operations;
7. linked reminder presets and event reconciliation;
8. Tizen alarm/notification adapter and privileges;
9. Schedule generated binding and provider implementation;
10. Reminder list/editor UI;
11. full host/build/package/device verification;
12. candidate-B visual regression and durable evidence update.

## 10. Explicit scope limits

Included now: touch/pointer, full local event CRUD, event-linked reminder presets, independent reminders, persistence, Calendar Actions, Schedule Actions, alarm notifications.

Still deferred: recurrence rules, attendee invitations, account/cloud sync, Samsung Account integration, timezone editing UI, attachment support, physical-TV validation, and external reminder-app interoperability.
