# Reminder API 14 build and target validation

Run commands from the repository root. The final stage 1 results and environment
are recorded in [STAGE1_VALIDATION](../../Calendar/docs/STAGE1_VALIDATION.md).

## Contracts and prerequisites

Use .NET SDK 8, Tizen Studio/signing tools, actionc and its action2tidl/tidlc
backends, and the current `tizen-action/default-actions` catalog. App and package
ID remain `org.tizen.actionexamples.reminder`. The manifest declares .NET API 14;
projects reference `Tizen.NET 14.0.0.19326`.

Generate complete Reminder (5 methods), ReminderCustom (6 methods), and View
(4 methods) categories. The Schedule-named project is an internal historical
project name; it no longer advertises `Tizen.Action.Schedule`. Do not modify
generated C# or platform-owned schemas. Custom v1 dispatch follows the runtime's
alphabetical fallback: AddRecording, AddViewing, CancelRecording, CancelViewing,
GetReminderByIds, GetReservations. Preserve these slots when evolving the ABI.

```sh
python3 Reminder/generate-bindings.py
bash Reminder/build.sh all
bash Reminder/package.sh
```

Package output is `Reminder/dist/org.tizen.actionexamples.reminder-0.1.0-api14.tpk`.
The packaging helper checks manifest, application payload, Custom action resources
and TPK signatures. Its emulator signing fallback is for local Common Emulator
validation. Product signing remains a separate deployment concern.

To prove generation is reproducible, generate into a fresh temporary directory
with `--output-root`, then compare all three generated `.cs` files byte-for-byte.
Host suites run from the Reminder directory (the contract suite resolves the sibling catalog relative to that directory):

```sh
(cd Reminder && dotnet run --project tests/Reminder.Core.Tests)
(cd Reminder && dotnet run --project tests/Reminder.ActionProvider.Tests)
```

## Installed target

Use an explicitly selected, authorized target and a matching Action runtime.

```sh
sdb -s emulator-26111 install Reminder/dist/org.tizen.actionexamples.reminder-0.1.0-api14.tpk
sdb -s emulator-26111 shell 'app_launcher -s org.tizen.actionexamples.reminder'
python3 Reminder/tests/prepare_target_validation.py --output /tmp/reminder-scenarios
sdb -s emulator-26111 push /tmp/reminder-scenarios/01_actions.json /tmp/reminder-actions.json
sdb -s emulator-26111 shell 'action-tool run --json /tmp/reminder-actions.json' > /tmp/reminder-results.json
python3 Reminder/tests/check_target_results.py /tmp/reminder-results.json /tmp/reminder-scenarios/run.json
```

The 33-step scenario checks standard CRUD/state transitions, resolver order and
duplicates, bounded failures, current-state presentation and actual display,
and simulated reservation add/cancel/query. Compare the four reservation query
responses with the run's owned IDs: viewing; viewing+recording; recording; empty.
Existing unrelated reservations must remain unchanged. No-input GetReservations
has no invalid argument; its bounded failure checks a missing provider route.

Mutation postconditions use standard Search or Custom resolvers/GetReservations.
The scenario removes only its generated IDs. If interrupted, inspect `run.json`
and use the generated `02_cleanup.json` for those IDs only.

## View and native UI

Use the repository Aurum skill to foreground the app, focus a visible control,
inspect native screenshots and measure controls. Then:

```sh
python3 scripts/validate-provider-views.py --serial emulator-26111 \
  --app reminder --display --output /tmp/reminder-views.json
```

This checks 20 consecutive list/find pairs, finite positive measured bounds,
current focus, missing/oversized and mismatched IDs, authoritative current data,
and the actual View-to-Display round trip. The output uses the legacy A2UI v0.8
split Template/Document compatibility profile, not canonical v0.9.1.

Exercise create/save, edit, complete, search, delete confirmation/cancel, D-pad,
pointer and Back separately. Check FHD/UHD/DCI 4K/8K WindowSize, insets, actual
View bounds and exact native screenshot dimensions. For large raw Aurum frames
use `TIZEN_AURUM_MAX_MESSAGE_MIB=160`; see the Aurum operations reference.
Common reservation behavior is an app-owned simulator. Real TV tuner/recording,
product profile acceptance and hardware touch require their own target tests.
