# Reminder build and E2E guide

## Evidence boundaries

Keep these results separate:

1. Host tests prove Tizen-free domain, persistence, use-case, simulator, and static generated-contract invariants.
2. .NET builds prove generated bindings, providers, and NUI compile with Tizen.NET 13.
3. Package inspection proves manifest/payload/signature structure.
4. Tizen 10.1 Common Emulator E2E proves provider discovery, RPC wire behavior, persistence restart, NUI focus, and actual View geometry.
5. Common reservation results are **simulated**. Real viewing/recording remains a TV-product gate.

## Prerequisites

- .NET SDK 8
- `actionc 0.1.0`, `action2tidl`, and `tidlc 2.9.0` on `PATH`
- authoritative catalog at `/home/hjhun/samba/workspace/appfw/tizen-action/default-actions`
- Tizen Studio with a Tizen 10.1 Common x86_64 emulator and signing profile for packaging

Do not modify the catalog or generator repositories for this app.

## Host verification

Run from `Reminder/`:

```sh
dotnet run --project tests/Reminder.Core.Tests/Reminder.Core.Tests.csproj
dotnet run --project tests/Reminder.ActionProvider.Tests/Reminder.ActionProvider.Tests.csproj
dotnet build src/Reminder.App/Reminder.App.csproj --configuration Release
```

Expected test markers:

```text
Reminder.Core.Tests: PASS (30 assertions)
Reminder.ActionProvider.Tests: PASS (10 MethodIds/implementations/metadata + current View contract)
```

The generated provider assemblies depend on Tizen runtime libraries. Their real dispatch path is therefore an emulator RPC gate, not a host-process unit-test claim.

## Reproduce generated sources

Generate complete categories; never use a Schedule subset:

```sh
CATALOG=/home/hjhun/samba/workspace/appfw/tizen-action/default-actions
TMP=$(mktemp -d)
ACTIONC_ACTION2TIDL=$(command -v action2tidl) \
ACTIONC_TIDLC=$(command -v tidlc) \
actionc -a Tizen.Action.Schedule -d "$CATALOG" -l 'C#' \
  -o "$TMP/ReminderScheduleActionProvider"
ACTIONC_ACTION2TIDL=$(command -v action2tidl) \
ACTIONC_TIDLC=$(command -v tidlc) \
actionc -a Tizen.Internal.Action.View -d "$CATALOG" -l 'C#' \
  -o "$TMP/ReminderViewActionProvider"
```

Compare each output with its checked-in `Generated/*.cs`. The only permitted difference is the same Tizen.NET 13 compatibility guard used by Calendar around `HasPrivilegeLocal`: when the API is unavailable, it sets `has = false` and denies access. Schedule MethodIds must remain `2..11` in authoritative `action.seq` order; View MethodIds must remain `2..5`.

## Package

The manifest uses package/app ID `org.tizen.actionexamples.reminder`, Common profile, API baseline 10.0 (compatible with the 10.1 target), and .NET API 13. Follow the same Tizen CLI/signing flow as Calendar from `src/Reminder.App/`:

```sh
tizen build-cs -C Debug -- .
# Stage the top-level build output plus tizen-manifest.xml, then package it:
tizen package -t tpk -s <signing-profile> -o <output-dir> -- <staging-dir>
```

Before install, inspect the TPK and confirm it contains `Reminder.App.dll`, all five referenced Reminder assemblies, `tizen-manifest.xml`, and the expected signature files. Packaging commands can vary by installed Tizen Studio CLI; record the actual CLI version and command used.

The packaged layout produced by Tizen CLI 2.5.25 places the entry assembly at `bin/Reminder.App.dll` and referenced assemblies under `lib/`.

## Common Emulator registration

1. Verify the target is Tizen 10.1, profile `common`, and x86_64 with the matching SDK `sdb`.
2. Install the newly built TPK.
3. Explicitly preload registration; install alone is not discovery evidence:

```sh
sdb shell 'tpk-backend --preload -y org.tizen.actionexamples.reminder'
```

Require a genuine installer success marker, then independently verify package/app discovery and that all 10 exact Schedule metadata rows plus four internal View rows select `org.tizen.actionexamples.reminder`.

## Schedule Action matrix

Invoke the real generated wire path with explicit sample app ID/provider selection. For every method, capture one success and one bounded failure:

| Method | Success | Bounded failure | Postcondition |
|---|---|---|---|
| AddRecording | future valid recording ID | kind mismatch/end <= start | GetReservations has recording ID |
| AddViewing | future valid viewing ID | past start | GetReservations has viewing ID |
| CancelRecording | existing recording | viewing ID | recording removed, viewing retained |
| CancelViewing | existing viewing | recording ID | viewing removed, recording retained |
| CompleteReminder | active ID | unknown ID | completed Search result; no active alarm handle |
| CreateReminder | valid caller-supplied ID | blank title/conflicting duplicate | Search finds exactly one ID |
| DeleteReminder | existing ID | malformed/oversized ID | Search no longer finds ID |
| GetReservations | mixed valid reservations | provider unavailable fixture | start-time/ID deterministic order |
| SearchReminder | keyword/category/limit | unknown category/limit > 100 | response count bounded and deterministic |
| UpdateReminder | valid replacement | unknown ID/invalid date | Search exposes replacement snapshot |

Accepted `Status.Reason` failure prefixes are `invalid:`, `not_found:`, `conflict:`, `unavailable:`, and `internal:`. Create/Add are caller-ID idempotent: same ID and payload succeeds without duplication; different payload conflicts.

Restart the app after mutations and verify persisted IDs/completion/reservations restore. Common reservation success must remain labeled `Common-simulated`.

## NUI and View E2E

The proportional viewport helper is covered at 1920×1080, 1280×720, 1440×1080, and 2560×1080 by host tests. Native UI evidence currently covers 1920×1080; before a device-profile release, repeat the following native checks at both 1920×1080 and 1280×720:

1. Confirm left Today/Upcoming/Overdue/Completed/All/Reservations navigation, center bounded list, and right detail/editor are visible without horizontal scrolling.
2. Exercise D-pad/keyboard and pointer for section selection, add, list selection, edit/save, complete/delete, reservation add/cancel, Back, and focus restoration.
3. Verify focus has visible selection background plus native focus treatment and accessibility names are present.
4. Query `GetAnnotatedViews`; only rendered cards/detail entities may appear.
5. Require positive finite `ScreenBounds`; when available, require matching positive `WindowBounds`.
6. Require `Annotation.EntityType`, stable `EntityId`, and generated `EntityInfo`. List Reminder projections omit note; selected detail may include visible note.
7. Move actual focus to a list card and verify `GetFocusedView` returns the same entity ID.
8. Call `FindById` and compare bounds/identity with `GetAnnotatedViews`.
9. Call `ToPresentation`; parse Template and Document independently and require `surfaceUpdate` and matching `dataModelUpdate`.
10. Pause/background the app and require zero annotations; resume and require freshly measured bounds.

`FindById` has scalar input schema `{ "id": "<view-id>" }`; do not pass the full View entity. `ToPresentation` takes the View entity directly. On this Action framework build, a no-input Action can set the outer MCP-style `isError` flag even while returning a valid non-empty `structuredContent`; judge the typed `return.Success` status rather than that outer compatibility flag.

Do not report host build, package install, synthetic simulator behavior, or screenshots as substitutes for these runtime checks.
