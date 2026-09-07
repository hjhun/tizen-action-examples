# Canonical application identities — 2026-09-07

Calendar and Reminder now use the same canonical identifier for both their
package and UI application:

| Application | Previous package / app ID | Current package / app ID |
|---|---|---|
| Calendar | `org.tizen.actionexamples.calendar` | `org.tizen.calendar` |
| Reminder | `org.tizen.actionexamples.reminder` | `org.tizen.reminder` |

PhotoGallery (`org.tizen.photogallery`), DisplayPresentation
(`org.tizen.displaypresentation`) and Browser (`org.tizen.browser`) already use
the canonical format in their source manifests. PhotoGallery and
DisplayPresentation were already installed with those IDs; Browser was not
installed on this target and is outside this two-package migration.

## Contract and packaging

The change updates the manifests, eight app-owned Custom Action `details.appid`
entries, application-qualified Search categories, package output paths and
current build/validation instructions. Callers must address the new app IDs.
The stable domain category names (`Tizen.Action.Calendar` and
`Tizen.Action.Reminder`), Action method IDs, Entity IDs and persistence formats
are unchanged. Dated validation records retain their historical identifiers.

Complete actionc regeneration against the existing catalog produced byte-identical
output for all seven Calendar/Reminder bindings. Generated source was not edited.
The public C# reference remains `Tizen.NET14.0.0.19326`; each UI application declares
API 14, and the existing minimum platform manifest version remains `10.0`.
The local tizenfx checkout is clean at Gerrit `tizen_10.1` revision
`6837507e05e22a4f5f4e76f532bd42b583d26709`, re-fetched and compared with
`FETCH_HEAD` on 2026-09-07. Both apps remain .NET/NUI implementations. Source
inspection found no app-owned C/C++ implementation or `DllImport`,
`LibraryImport`, `NativeLibrary` or separate Interop implementation in their
source trees. Providers still inherit the seven generated `ServiceBase` classes;
platform native internals behind public C# APIs are not app-owned code.

Build and package from the repository root:

```sh
python3 scripts/check-app-identities.py
./Calendar/build.sh
./Reminder/build.sh
./Calendar/package.sh
./Reminder/package.sh
```

Outputs are `Calendar/dist/org.tizen.calendar-0.1.0-api14.tpk` and
`Reminder/dist/org.tizen.reminder-0.1.0-api14.tpk`.

## Installed migration and data preservation

Target: `emulator-26111` / `tc-0905-actionagent`, Tizen Common x86_64, FHD
1920×1080. Before removal, both old apps were stopped and their JSON data stores
were backed up to a private local directory outside the repository. The old
Calendar held **four events and one reminder**; the old Reminder held no reminder
or reservation records.

The old packages were removed with the requested commands:

```sh
sdb -s emulator-26111 shell 'pkgcmd -un org.tizen.actionexamples.calendar'
sdb -s emulator-26111 shell 'pkgcmd -un org.tizen.actionexamples.reminder'
tizen install -s emulator-26111 -n Calendar/dist/org.tizen.calendar-0.1.0-api14.tpk
tizen install -s emulator-26111 -n Reminder/dist/org.tizen.reminder-0.1.0-api14.tpk
```

Uninstall returned `key[end] val[ok]` for both old packages. Package lookup then
reported both old IDs absent. Both new packages installed successfully.

For this authorized emulator migration, the backed-up documents were copied to
the new app-owned data directories before first launch, with ownership and Smack
labels matching those directories. Only obsolete alarm/resource handles were
cleared in the restore copies; the originals remain intact in the private backup.
Startup used the existing public-API restoration path to reconcile resources.
All persisted content and Entity IDs matched after restoration, excluding the
reconciled handles. This is a deployment migration, not a cross-app data-reading
feature or an Interop implementation in the apps.

## Verification

- RED: the canonical manifest/Custom metadata check failed on the old IDs, and
  Calendar/Reminder host query regressions rejected their new app-qualified
  category. All three passed after the change.
- Source identity check: all five app manifests and all app-owned Action IDs pass.
- Both API 14 builds: zero warnings and errors. TPK checks verify canonical
  package/app IDs, API metadata, signature entries and exact Custom resource bytes.
- Calendar ActionProvider, Reminder Core and Reminder ActionProvider host suites
  pass; Calendar's schema/ABI/manifest contract checker also passes.
- action-tool provider discovery returns `org.tizen.calendar` for Calendar and
  `org.tizen.calendar`, `org.tizen.reminder` for Reminder. Both apps intentionally
  provide standard Reminder Actions.
- All eight Custom default-provider entries automatically changed to the new
  IDs on package registration; no unrelated platform default was altered.
- Installed Action scenarios: Calendar **37** and Reminder **33** steps pass,
  including success, bounded failure and mutation query postconditions. Their
  cleanup scenarios (**6** and **3** steps) also pass.
- Default-route regression: the same 37/33-step scenarios also pass with `appid`
  omitted from all valid CalendarCustom/ReminderCustom calls. All **eight** Custom
  methods are exercised through their registered defaults. The intentional
  missing-provider failure retains its explicit invalid app ID. Cleanup passes.
- Identity-specific target checks: **38 PASS**. These cover canonical package/app
  lookup, removal of old packages/providers, canonical Search categories, exact
  typed rejection of old categories, five restored entities queried by stable ID,
  all eight registered Custom metadata/default mappings, and default RPC routing.
  Old-ID RPCs both return JSON-RPC 2.0 `result.isError=true`, empty
  `structuredContent`, text `Failed to start the TIDL action plan`, and target
  exit **1** (SDB transport exits 0). Along with package/provider absence, this
  proves the obsolete routes cannot start. Parsing errors, transport failures,
  or arbitrary error text do not count as a pass.
- Final persisted documents match the pre-install restore copies in all fields
  except deliberately reconciled alarm/resource handles. Four Calendar events
  and one Calendar reminder retain their stable IDs; Reminder remains empty.
  Scenario and UI fixtures are removed.

On **2026-09-07**, after installing the canonical packages on
**emulator-26111 / tc-0905-actionagent**, the native FHD UI regression passed
**155 checks (Calendar 69 / Reminder 86), exit 0**, with **64 unedited native
captures**. It covers live Add/Update/Delete visibility and Search postconditions,
20 consecutive annotation snapshots per app, current View-to-Presentation and
installed DisplayPresentation rendering, draft/invalid-date/input-focus retention,
pause/resume, Calendar's Reminder subflow, Reminder viewing/recording reservations
and D-pad focus/restoration. App IDs changed; the UI/scaling structure did not.

Local evidence is retained privately under `/tmp/provider-identity-0907`:
`ui-acceptance/results.json`, `ui-acceptance/*.png`, `ui-acceptance.log`,
`identity-target-checks.json`, `identity-rpc-evidence.json`, both app build/install
logs, and explicit/default-route scenario reports. RPC evidence preserves raw
stdout/stderr, the parsed envelope, trailing text, SDB status and target exit
status. These local raw records may contain existing user data and are excluded
from Git. The original verifier parsing failure and the default-scenario fixture
error are retained as failed attempts; only corrected complete runs count above.

Native UI uses the unchanged implementation from the
[live refresh validation](LIVE_REFRESH_VALIDATION.md). Its reusable verification
script now addresses the canonical IDs:

```sh
python3 Shared/tests/verify_live_refresh.py --serial emulator-26111 \
  --port 55061 --output /tmp/canonical-app-ui
```

## Advertised Actions (unchanged)

| Provider | Category | Methods |
|---|---|---|
| Calendar | `Tizen.Action.Calendar` | AddEvent, DeleteEvent, Search, ToPresentation, UpdateEvent |
| Calendar and Reminder | `Tizen.Action.Reminder` | Add, Delete, Search, ToPresentation, Update |
| Calendar and Reminder | `Tizen.Action.View` | FindById, GetAnnotatedViews, GetFocusedView, ToPresentation |
| Calendar | `Tizen.Action.CalendarCustom` | GetEventByIds, SearchInPeriod |
| Reminder | `Tizen.Action.ReminderCustom` | AddRecording, AddViewing, CancelRecording, CancelViewing, GetReminderByIds, GetReservations |

## Resolution evidence and remaining limits

Host and target gates are separate. This identity migration reran the affected
query/provider host suites and the FHD native UI suite. The existing full geometry
TDD and higher-resolution native results are retained from
[stage 1, 2026-09-06](STAGE1_VALIDATION.md) and
[live-refresh host regression, 2026-09-07](LIVE_REFRESH_VALIDATION.md).

| Resolution | Host geometry | Native target evidence | New-ID migration run |
|---|---|---|---|
| FHD 1920×1080 | PASS, scale 1 | Stage 1 and live-refresh PASS | 155 UI checks PASS; scale/insets implementation unchanged |
| UHD 3840×2160 | PASS, scale 2 | Stage 1 PASS; measured 3840×2160 canvas | Not rerun at UHD |
| DCI 4K 4096×2160 | PASS, scale 2 / x=128 | Stage 1 PASS; 3840×2160 canvas centered with pillarboxes | Not rerun at DCI 4K |
| 8K 7680×4320 | PASS, scale 4 | **Unverified on target due to emulator DRM constraint** | Not rerun on target |

Both apps read public SystemInfo (`Tizen.System.Information.TryGetValue` screen
features) at initialization and public NUI `Window.Default.WindowSize` /
`GetInsets()` for the drawable area. On this emulator, SystemInfo reported
1280×720 even when the measured window was larger. Consequently the actual
window minus insets determines one centered uniform ancestor transform;
SystemInfo records display capability rather than forcing the canvas size.
Typography, radii, borders and focus use reference design units scaled once.
Stage 1 native bounds and keyboard-inset tests establish that distinction; this
ID-only change does not alter those calculations.

Physical TV/product validation and native 8K remain unverified. Reservation
operations remain the app-owned Common Emulator simulator, not real broadcasts.
Presentation uses the previously verified legacy A2UI v0.8 compatibility profile;
this work does not establish canonical v0.9.1 or transparent-overlay support.
Aurum accessibility-tree and physical-touch coverage are not claimed.

Historical README/UI_PARITY screenshots retain the app IDs and dates under which
they were captured. In particular Calendar's 2026-08-08 README images retain
`org.tizen.actionexamples.calendar` provenance; current installation evidence is
recorded separately here. Build outputs, TPKs, backups, raw logs and temporary
captures are excluded from the commit. Existing DisplayPresentation and
graphify-out work, and the unrelated DisplayPresentation dashboard hunk, remain
outside this identity migration.


Final cleanup removed only this migration's remote scenario directories and
Aurum forwarding `55061 -> 50051`; pre-existing forwarding `55219 -> 5219` and
the shared bootstrap were preserved. Both canonical apps were relaunched to
clear test-only UI search/editor state. The private recovery backups remain
outside the repository; no VM or other agent session was removed.
