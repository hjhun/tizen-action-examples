# Calendar / Reminder live refresh — 2026-09-07

## Observed problem and correction

Calendar's shared command service saved Action mutations without notifying the
running UI. Before this fix, an AddEvent succeeded and Search returned the new
event, but the visible page and ViewAnnotations still contained no matching
entity. Reminder already posted a change event; its full render discarded live
editor controls. Typing `q`, then invoking Update through action-tool, reproduced
the lost draft on the installed app.

Both applications now receive committed changes through a coalescing UI dispatcher
using the public C# synchronization context. Calendar publishes its change event
after persistence and both shared repositories are current. Observer exceptions
cannot report an already committed mutation as failed or compensate its resource.
Queued refresh callbacks are discarded after termination.

Calendar retains its editor/search actors and replaces the underlying month and
applied search results. Reminder replaces data rows and the saved detail while
retaining live editor/search actors. This preserves partially entered dates,
text, native input focus and cursor state. Rebuilt controls recover focus by their
stable identity; a removed Reminder control falls back to a visible row or Search.
Resume uses the same refresh path. Intermediate actor changes do not publish
annotations; the completed live tree replaces the snapshot and is measured again
  after layout.

Repeated Calendar reminder completion buttons also use the reminder ID in their
control identity, so every visible row is annotated and keeps its own focus target
when other rows move or change completion state.

The active date, smart list, applied query and time filter remain selected.
Consequently a tomorrow item does not appear in Today, and an item without a due
time belongs in All / No alert. Unsubmitted search text stays a draft; live results
continue to use the last applied query. This is intentional filtering, not a stale
repository. Saving a Reminder editor whose item was externally deleted returns a
missing-item error and retains the draft.

## Verification layers

- **RED:** Calendar host regression failed because the committed-change event was
  absent. Reminder host regression failed when a throwing observer converted a
  committed update into failure. Installed baseline reproduced Calendar's missing
  row/annotation and Reminder's lost editor draft.
- **Host GREEN:** Calendar Domain, Persistence, UseCases, App and ActionProvider;
  Reminder Core and ActionProvider suites. New cases cover commit ordering,
  rejected/failed persistence, observer isolation and resource preservation,
  provider-thread dispatch, burst coalescing and termination. Existing annotation,
  search, persistence and FHD/UHD/DCI 4K/8K geometry cases also pass.
- **API 14:** Both app builds complete with zero warnings and errors. tizenfx
  `tizen_10.1` was fetched and checked at
  `6837507e05e22a4f5f4e76f532bd42b583d26709`; SDK reference package is
  `Tizen.NET14.0.0.19326`. No generated binding source or schema was changed.
- **Package:** Both API 14 TPKs were packaged, checked and installed on
  `emulator-26111` before Action/UI verification. The existing platform manifest
  minimum remains `10.0`; the public C# reference API is independently versioned
  `14.0.0.19326`.
- **Native:** action-tool mutations are issued while the actual NUI page is open.
  Search/GetReservations verify persisted postconditions; View Actions verify
  current entity snapshots, input drafts and measured focus. Aurum captures each
  changed state, including the native keyboard and inset-driven canvas scaling.
  See the reproducible script below and each app's UI parity record.

## Reproduction

Final isolated run: **155 checks PASS, exit 0**, with 64 native FHD captures.
Calendar contributes 69 checks and Reminder 86. The counts include 20 consecutive
current annotation snapshots per app after presentation return. Interrupted runs
from earlier shared-target activity are excluded from this result.

| Surface / contract | Final installed result |
|---|---|
| Calendar event / Reminder create, update, delete | Visible data and annotations update; Search verifies each persisted mutation |
| Invalid update | Typed failure and unchanged saved data |
| Editor + background/resume | `q` title, invalid date/time containing `x`, and input focus retained |
| Calendar applied search / Reminder search draft | Results refresh using applied query; unapplied `z` remains in the input |
| Calendar standalone reminders | Multiple completion controls, current state, editor draft and D-pad focus retained |
| Reminder external deletion during edit | Editor retained; Save reports missing item without recreating it |
| ReminderCustom viewing / recording | Add/cancel reflect in list and detail, invalid range rejected, GetReservations postconditions pass |
| D-pad / removed detail control | Right enters reservation action; external cancellation restores visible Search focus |
| View → Presentation → Display | Updated title reaches the actual installed renderer for both apps |

Selected unedited native evidence:
[Calendar current presentation](images/calendar-live-refresh-display.png),
[Reminder live draft](../../Reminder/docs/images/reminder-live-refresh-draft.png),
[deleted-item editor](../../Reminder/docs/images/reminder-live-refresh-deleted-editor.png),
[reservation cancellation focus](../../Reminder/docs/images/reminder-live-refresh-focus.png).
These captures contain only this validation's fixtures. Other captured Calendar
frames include pre-existing target content and remain outside the repository.

Build/package/install both applications with their existing scripts, start the
repository Aurum session against the intended target, then run from the repo root:

```sh
.agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui session-start \
  --serial emulator-26111 --port 55061
python3 Shared/tests/verify_live_refresh.py --serial emulator-26111 \
  --port 55061 --output /tmp/provider-live-refresh
.agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui session-stop \
  --serial emulator-26111 --port 55061
```

The script creates uniquely identified fixtures and deletes only those fixtures
in `finally`. It writes native captures and an assertion record to the supplied
output directory. Only an exit status of zero is a passing complete run; a partial
assertion record from a failed run is not a pass. Both apps, DisplayPresentation
and PhotoGallery (used to cover the editor during lifecycle testing) must be
installed. The fixture due
time is derived from the guest clock and should still be on the selected day.
Use `--app calendar|reminder` or `--related-only` for a scoped repeat.

## Native scope and limits

Target: `emulator-26111` / `tc-0905-actionagent`, Tizen 10.1 Unified Common x86_64,
1920×1080, tested 2026-09-07. Aurum's accessibility tree is empty on this profile;
input uses native coordinates from measured ViewAnnotations and remote key RPCs.
The visible Back/Home and keyboard are platform surfaces. The first Back can be
consumed by the keyboard, so editor cancellation uses the visible Cancel/Close.

An action-tool provider RPC can itself request application resume on this runtime.
The lifecycle test therefore captures the covered frame, changes data, returns to
the app and verifies the retained draft/current data. It does not infer a stable
paused-state RPC result from a short delay.

This fix retains the existing reference-canvas geometry; native UHD/DCI 4K tests
were not repeated for the refresh change. Their accepted evidence remains in
[stage 1](STAGE1_VALIDATION.md). 8K geometry passes on the host;
**Unverified on target due to emulator DRM constraint**. Physical TV remote/touch
and real tuner/recording remain separate unverified product gates. ReminderCustom
reservations use the existing explicitly labeled Common Emulator simulator.
