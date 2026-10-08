# Canonical Action 1.8 compatibility

[한국어](CANONICAL_ACTION_1_8_COMPATIBILITY_ko.md)

Browser, Calendar, Reminder, PhotoGallery, and DisplayPresentation use the
canonical default schemas and `action.seq` from tizen-action 1.8 source
`7ce2a4ca3c0839986d8c6abc8856efaf59f1d3b6`. Their 15 generated files cover
complete categories and custom-category dependencies. Positional method IDs
must follow the complete category, including methods the app cannot implement.
Never hand-edit generated bindings or reorder `action.seq`.

Regeneration uses actionc/action2tidl from toolchain source
`f15c1cda3c985c537addb05161f45518103dcc30` and tidlc 3.1.3, with protocol 3
optional presence frames. Select the matching tools explicitly through
`ACTIONC_BIN`, `ACTIONC_ACTION2TIDL`, and `ACTIONC_TIDLC`; select the canonical
catalog and sequence through `ACTIONC_DATA_DIR` and `ACTIONC_ACTION_SEQ`.
The generation input is a byte-identical complete-category dependency closure,
including each app's custom schemas. Run each app's `build.sh generate`, then
build separately. The old PATH tidlc 2.10.2 is not the matching compiler.

## Implemented boundary

The regenerated ABI does not add app features. The following methods return
explicit unavailable before domain mutation and are not advertised:

| App | Unavailable canonical methods |
| --- | --- |
| PhotoGallery | AddPhoto, DeletePhoto, StartSlideshow, GetMemories, PlayMemory |
| Calendar | Calendar AddEvent/UpdateEvent; Reminder Add/Update |
| Reminder | Add/Update |
| DisplayPresentation | ShowNudge |

Retired ToPresentation and Presentation Show contracts are removed from native
providers and manifests. Existing app UI and business behavior remain intact;
Display's local annotation preserves its legacy wrapped JSON and is not a
canonical 1.8 Presentation entity or a ShowNudge implementation.

Unsupported filters reject by presence, including an explicitly empty value or
list. No filter is silently ignored. Omitted Reminder State selects reminders
not yet Done; if any selected state cannot be represented by canonical To-do or
Done, Search rejects the whole result before limiting. The custom ID resolver
also rejects unrepresentable states without partial results or unresolved-ID
substitution. Supported ID order and duplicates remain intact. An absent due
date remains null. Optional failure outputs are absent; required failure
outputs are initialized and paired with Success=false.

## Managed View ownership

All five apps set `DALI_DISABLE_ENTITY_DATA_TIDL=1` through the checked native
`Tizen.NUI.EnvironmentVariable` API at the beginning of Main, before application
construction. This process-scoped adaptor opt-out leaves the explicit managed
TizenActionView listener as the app's owner. It does not change a global service
or repair the native built-in provider's older wire contract.

Exact installed adaptor/toolkit source confirms launchpad preload creates the
adaptor and warms controls without starting its EntityData host. The host is
otherwise created during adaptor Start before managed OnCreate. The installed
NUI setter was independently checked against libc getenv.

Canonical View requires WindowBounds. Display retains actual absent geometry
in its snapshot but returns unavailable before publishing an unrepresentable
View. Annotated-view queries reject the whole list; lookup/focus queries return
initialized failure Views. No visible view is silently dropped, and no successful
coordinates are fabricated.

## Validation limits

The initial compatibility increment passed 20 portable suites and four provider
return-value lanes with genuine installed managed assemblies. The geometry
follow-up passed its focused host regression; all five owner builds and package
commands passed, followed by six relevant portable app/Display suites. Host
provider tests do not exercise Parcel/native transport.

On the emulator, the final installed packages matched all 31 DLLs and five
manifests. All five cold sole-View calls and five ordinary-launch then View calls
were accepted and delivered final replies with the exact managed failure reason,
empty Id/Extra, and valid required objects (10/10). The prior Display cold launch
wrong-handler defect is resolved for these app-owned providers. A public watcher
registered before the package updates received UPDATE and RESYNC events; the
resident Argot PID remained 3082 and its ready catalog remained 66 after updates.
Receipts: `argot-capmgr-csharp-owner-device-check.json` and
`argot-capmgr-csharp-owner-update-watch.log`.

The final readonly catalog/action rerun passed all ten expected results:
nonempty Browser Tab/Page, valid managed Display missing-View failure, and
Photo/Calendar/Reminder empty successes with unsupported-filter failures.
Receipt: `argot-capmgr-csharp-owner-final-device-receipt.json`. Empty results do
not establish nonempty entity or UI behavior. The tested installed native built-in View serializer
remains unchanged; this app-scoped ownership result does not prove a
general framework repair or full visual UI acceptance.
