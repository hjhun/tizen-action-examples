> 이 문서는 마이그레이션 중간 기록입니다. API 14 타깃 설치·Action·View·UI 최종 결과와 Reminder 마이그레이션은 [1단계 검증 기록](STAGE1_VALIDATION.md)을 따릅니다.

# .NET Calendar update — 2026-09-06

[한국어](2026-09-06-dotnet-update.md)

## Scope and sources

Follow-up: DisplayPresentation now accepts the actual Calendar 0/1/2/7/100-event
legacy explicitList/nested bindings and pages four text fields at a time.
Template/Document each have a 65,536-code-unit transport limit independent of the
event count. See the [integration record](STAGE1_VALIDATION.md)
for three-app host/build/package results and prepared target scenarios.

Updated .NET/NUI Calendar display sizing and Action contracts, plus page annotations in Calendar and Reminder. Removed `Calendar/native` as requested. The separate Reminder app retains its existing Schedule/API 13 contract in this annotation change.

- Action source: `~/samba/workspace/appfw/tizen-action/default-actions`, revision `4355566aa851470dc290c69672ba1f8bdd8470e7`, inspected 2026-09-06.
- TizenFX source: `~/tizen/platform/core/csapi/tizenfx`.
- Calendar reference: `Tizen.NET 14.0.0.19326`, dotnet manifest API 14. Generated API 14 `HasPrivilegeLocal` calls compile without source patches.
- Preserved the existing Samsung Calendar-based Month/Week/Day/Agenda hierarchy. This work introduces no new design or verified Samsung product version. Screenshots from 2026-08-08 do not validate this update.

## Screen and window

Read `http://tizen.org/feature/screen.width` and `screen.height` through `Tizen.System.Information.TryGetValue()`, separately from `Window.Default.WindowSize`. The drawable area is the actual window minus `GetInsets()`. Physical screen dimensions do not determine the window dimensions.

Center the 1920×1080 reference canvas with one uniform ancestor scale and reference `PixelSize` typography. Full FHD/4K UHD/8K UHD windows use scales 1/2/4; an FHD window on an 8K screen uses 1. Independent axis scaling and screen-width-only typography were rejected because they distort or overflow smaller windows.

Missing screen information does not prevent a valid window layout. Invalid window/inset measurements retain the existing root; screen size is never substituted. Resize updates existing page/overlay canvas positions and scales, preserving unsaved text and focus. Bounds are measured again after layout.

## Action changes

| Previous | Current |
|---|---|
| `Tizen.Entity.Calendar` | `Tizen.Entity.CalendarEvent` |
| `Calendar_RemoveEvent` | `Calendar_DeleteEvent` |
| `Calendar_Search(Query)` | `Calendar_Search(CalendarQuery)` |
| `Query.Number` | `Query.Limit`, with Id/Category filters |
| Standard `Calendar_GetEventByIds` | `CalendarCustom_GetEventByIds` |
| Standard `Calendar_SearchInPeriod` | `CalendarCustom_SearchInPeriod(Calendar.Entity.SearchQuery)` |
| Single Calendar ToPresentation | Array of 0–100 CalendarEvents |
| Calendar's Schedule reminder methods | Standard Reminder Add/Delete/Update/Search/ToPresentation |
| Completed bool wire | `ReminderState.State`: To-do/In-progress/Blocked/Done |

The previous wire ABI is not retained; consumers must regenerate against the current schema. Persisted IDs and old bool completion JSON remain compatible, while all four states survive persistence.

Standard CalendarQuery.Id supports a single-ID search. The custom resolver preserves order, duplicates and unresolved IDs for up to 100 inputs. Custom SearchQuery extends CalendarQuery with title/location/note selectors; all false selects all fields. Generate whole standard categories in `action.seq` order and generate the custom category separately. Do not edit platform schemas or generated C# manually.

The custom v1 contract uses v2 Action definition files. Package definition/entity/provider metadata together with the original files under `res/`. UI and providers share the same repositories/use cases.

## Pages and focus

The [ViewAnnotation contract](VIEW_ANNOTATION_Eng.md) records every page and the target verification procedure. Page/control annotations use generated `TizenEntity` with version 1 state in `Extra`. Events and reminders use their generated standard entities. Live editor values belong to controls, without claiming to be saved entities.

Calendar overlays exclude the obscured background. Reminder editors retain the still-visible list and remove the replaced detail. Page, focus, input, layout, pause and resume changes replace the snapshot. Multiple visible instances of an event have separate View IDs and share an Entity ID.

Presentation binds current entity/page fields using the existing legacy v0.8 profile. This does not claim canonical v0.9.1 support or verified DisplayPresentation target round trips.

## Verification and remaining gates

| Layer | Result |
|---|---|
| Host | Five Calendar and two Reminder suites passed: ratios, 4K/8K, screen/window mismatch, persistence states, query/ID behavior, page/focus/snapshot lifecycle |
| Generated contracts | Whole-category method IDs/manifest checked; four regenerated files byte-identical |
| Build | Both .NET apps passed Release builds |
| Package | Calendar API 14 Common Emulator test-signed TPK passed ZIP, manifest, signature and custom resource checks |
| Target Action/UI | This update is not installed; real provider calls, 4K/8K rendering, input, Aurum and Presentation round trips remain unverified |

From Calendar, run `./build.sh all`, `python3 tests/check_action_contracts.py`, and `./package.sh`. The package is `dist/org.tizen.actionexamples.calendar-0.1.0-api14.tpk`. Actual native geometry/focus verification requires an authorized target installation. No commit or push was made.
