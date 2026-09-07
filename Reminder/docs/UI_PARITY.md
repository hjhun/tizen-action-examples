# Reminder API 14 preview / native UI evidence

Date: 2026-09-06. Canonical executable preview:
[`refs/one-ui-sample.html`](../refs/one-ui-sample.html).
The previous design-options HTML was removed. Requirements remain in
[REQUIREMENTS_DRAFT](REQUIREMENTS_DRAFT.md); the preview is an executable UI model.
See [stage 1 validation](../../Calendar/docs/STAGE1_VALIDATION.md).

2026-09-07 follow-up: [live Action refresh validation](../../Calendar/docs/LIVE_REFRESH_VALIDATION.md).
Installed rows and saved details refresh while editor/search controls remain
attached. Native Action changes, draft/focus preservation and reservation updates
are verified separately from the browser's local fixture behavior. The existing
three-pane geometry and visual styling are unchanged.
The [live draft capture](images/reminder-live-refresh-draft.png) shows the changed
saved row alongside retained `q` title text and an unfinished invalid due-time
value. The keyboard consumes the bottom inset; the whole canvas scales once.
The [deleted-item editor](images/reminder-live-refresh-deleted-editor.png) retains
input after Save reports the external deletion. The
[reservation cancellation capture](images/reminder-live-refresh-focus.png)
shows cleared detail/list data and focus restored to the visible Search action.

## Reference and adaptation

The [official Samsung Reminder guide](https://www.samsung.com/us/support/answer/ANS10003651/)
was inspected on 2026-09-06 for create/save, detail/edit, completed lists and delete
confirmation. An exact app/One UI binary version was not available from the page.
The existing smart-list/list/detail three-pane structure is the repository's
large-window and D-pad adaptation. It is not presented as a literal Galaxy screen.
Existing neutral/lavender colors and hierarchy are retained. Reservation simulator
copy stays explicit; real TV tuner/recording behavior is a separate product gate.

## Browser / installed comparison

The browser preview was exercised at 1920×1080 and 4096×2160: add, field entry,
save, detail, completion, delete confirmation/cancel and resize. Its DCI canvas
measures x=128, y=0, width=3840, height=2160, matching the native transform.

| Surface | Installed Aurum evidence | Comparison |
|---|---|---|
| Completed detail | [native](images/reminder-api14-completed.png) | Smart list, visible item, state and detail actions |
| Delete confirmation | [native](images/reminder-api14-delete.png) | Modal Cancel/Delete with background disabled |
| Dialog focus | [native](images/reminder-api14-focus.png) | Purple border and enlargement; arrows trap focus, Back cancels |
| View presentation | [native renderer](images/reminder-api14-display.png) | Current section/filter fields reach DisplayPresentation |
| UHD | [native](images/reminder-api14-uhd.png) | Uniform 2× layout/type/focus |
| DCI 4K | [native](images/reminder-api14-dci.png) | Same 2× canvas with 128 physical pixels on each side |

The preview uses browser typography, local fixtures and browser focus/input.
Native NUI text fields, virtual keyboard and alarm/simulator backend are not
implemented by the HTML. Browser metrics therefore differ; the installed capture
is authoritative. Historical 2026-08-09 API 13 screenshots in the README document
page structure only and are not final API 14 runtime evidence.

## Capture provenance and limits

- App: `org.tizen.actionexamples.reminder`, API 14 TPK.
- Native FHD: emulator-26111 / tc-0905-actionagent; UHD/DCI: isolated
  provider-resolution-0906 / emulator-26101; Tizen 10.1 Unified 20260905.101134
  Common x86_64 base image.
- Repository Aurum screenshot RPC, remote keys and calibrated native coordinate
  clicks. Tree root_count=0; no semantic-tree lookup claim. Native focus and
  bounds were checked through View Actions; Back/Home overlay is platform UI.
- Real keyboard input created fixture T, Search confirmed its ID, Complete changed
  State to Done, app terminate/relaunch retained it, delete/cancel restored focus,
  and final confirmed deletion produced an empty Search result.
- All committed PNGs are unedited native captures with decoded dimensions.
- 8K host geometry passes; native 8K is **Unverified on target due to emulator DRM
  constraint**. Physical TV remote/touch and real viewing/recording are unverified.
