# Installed integration acceptance

> Historical preparation record (2026-09-06). The serials, old
> `org.tizen.actionexamples.*` packages and Reminder API13 notes below describe
> that preparation only; do not run its installation commands against a current
> target. Current packages use canonical IDs in [Packages](../../Packages/README.md).
> Reconfirm device identity, current schemas and data preservation before any
> separately authorized target work. This source review performs no installation.

Prepared 2026-09-06; no installation or provider execution is claimed here.
Repository `AGENTS.md` requires explicit authorization to install packages on
external targets. The commands below are an execution plan after that approval.

## Local preparation

From the repository root, build/package Calendar and DisplayPresentation with
their `package.sh` scripts. Generate a new isolated set of requests:

```bash
python3 Calendar/tests/prepare_target_validation.py --output /tmp/calendar-interop-run
```

The output directory must not already exist. It contains exact `action-tool run`
scenarios, unique event/reminder IDs, and individual cleanup requests. It creates
no target data. `01_actions` will create an app-owned event, reminder and alarm;
cleanup must run even if a later assertion fails. Never delete unrelated data.

Whole-Entity Status results are JSON inside `result.content.0.text`; object
results use `result.structuredContent.return`. Scenario `expect.success` verifies
transport only. The generated assertions also check typed `Success`, and mutation
postconditions use Search/GetEventByIds. These paths follow the inspected local
action-tool source; installed execution remains the final wire-format check.

`Calendar_ToPresentation` receives `{"CalendarEvent":[...]}` (capital C/E and
singular property), whereas Search, Reminder and Presentation_Show take their
single Entity's fields directly. No generated entity-name wrapper is sent.

## Target sequence after authorization

Known candidate: `emulator-26111` (`tc-0905-actionagent`, Common Tizen 10.1 x86_64).
Confirm its identity again before installing. Install these local packages:

```bash
sdb -s emulator-26111 install Calendar/dist/org.tizen.actionexamples.calendar-0.1.0-api14.tpk
sdb -s emulator-26111 install DisplayPresentation/dist/org.tizen.displaypresentation-0.1.0-api14.tpk
```

1. Confirm discovery of Calendar's 16 and DisplayPresentation's five providers,
   including the two app-owned custom definitions. Use `action-tool schema` and
   a dry run to validate schema input separately from invocation.
2. Push this run's request files into an app-owned temporary target directory
   such as `/tmp/calendar-interop-<run-id>/`. Invoke each using
   `action-tool run --json /tmp/calendar-interop-<run-id>/01_actions.json` through
   `sdb shell`. Save stdout/stderr locally. Do not pipe stdin through `sdb shell`;
   that path did not complete on the inspected target.
3. Run `01_actions`: event create/update/search/resolver/presentation and bounded
   failures; all four reminder states with re-query and actual output → Show.
4. Launch Calendar, wait for layout and the 50 ms annotation publication timer,
   and focus an annotated control using Aurum. Run `02_calendar_views`. Its last
   step sends the actual View_ToPresentation result to DisplayPresentation.
5. Launch DisplayPresentation, wait for measured annotations and focus its enabled
   control. Run `03_display_views`. No same-tick assumption is made between Show
   and the next UI snapshot.
6. Run `04_cleanup` and verify empty Search/resolution. If an earlier scenario
   failed or only one fixture was created, run each individual `cleanup-*.json`
   via `action-tool execute --json -f <file>`; a missing ID is an expected typed
   failure. Then explicitly Search/resolve both run IDs to confirm absence.
   Remove only this run's temporary request directory.

Scenario failure must be investigated before changing expected values. A missing
provider, transport failure, typed failure and visual mismatch are different results.
The standalone Reminder package retains Schedule/API 13; its legacy SearchReminder
schema was absent from this target. Do not count Calendar's Reminder provider as
evidence for that separate app.

## Native UI and annotation checks

Capture equivalent HTML and installed NUI frames via the repository Aurum skill;
update [UI_PARITY.md](UI_PARITY.md) with actual image and trace paths.

| Flow | Required observation |
|---|---|
| Calendar 0/1/7/100 actual events → Show | Empty/single/multiple pages; all 400 short fields accessible at 100 events; no clipped fifth field |
| Previous/Next | Deterministic focus; disabled controls skipped; old page IDs disappear; snapshots exclude off-page values |
| Focus/View round trip | GetFocusedView agrees with actual focus; FindById agrees with discovery; serialized current page parses and renders again |
| Invalid/unsupported Show | Typed failure, bounded recovery text, Dismiss/Back enters empty state without old content |
| Resize/insets/move | FHD/720p/4K/8K and non-16:9 centered single scale; finite positive measured bounds; current page/focus retained |
| Pause/resume | Published views empty while paused; latest page republished after layout; no stale queued result resurrected |
| Reminder draft search | Typing keeps the displayed list and its first six Entity IDs; Search applies the keyword; clear requires Search; detail/editor/reservations keep visible identity |
| Input | D-pad/keyboard, pointer and touch exercised separately; Back/dismiss and external focus restoration recorded |

Canonical version/catalog lifecycle, richer negotiated semantics/actions, and
transparent hosting need separate implementation and acceptance. A successful
legacy round trip or browser screenshot does not prove those capabilities.
