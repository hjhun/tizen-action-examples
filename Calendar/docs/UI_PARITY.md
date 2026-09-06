# Calendar API 14 preview / native UI evidence

Date: 2026-09-06. Canonical executable preview:
[`refs/one-ui-sample.html`](../refs/one-ui-sample.html).
Requirements and acceptance remain in Markdown. [Stage 1 results](STAGE1_VALIDATION.md).

## Reference and adaptation

The existing Samsung Calendar-inspired Month/Week/Day/Agenda hierarchy, day-detail
pane, editing and search flows are retained. This change refines scaling and
interaction; it does not claim a newly inspected Samsung Calendar binary version.
The original repository Calendar screens from 2026-08-08 are historical references.
Colors and content hierarchy follow that existing app; the large-window/D-pad
layout is a Tizen adaptation. Music branding, media layout and assets were not used.

## Browser / installed comparison

The preview runs in a real Chromium session at 1920×1080 and 4096×2160.
Month/date selection, Add event, input/save, detail, delete/cancel and viewport
resize were exercised. At DCI size the canvas DOMRect is x=128, y=0,
width=3840, height=2160, matching both native page geometry and pillarbox policy.
The editor controls no longer crowd the fixed Save/Cancel row.

| Surface | Installed Aurum evidence | Comparison |
|---|---|---|
| Month | [native](images/calendar-api14-month.png) | Same command bar, 6-week grid and selected-day pane |
| Week | [native](images/calendar-api14-week.png) | Short single-line day headings; native title no longer clips year |
| Detail | [native](images/calendar-api14-detail.png) | Title/time/location/note with Edit/Delete side sheet |
| Editor | [native](images/calendar-api14-editor.png) | Same side-sheet hierarchy and save/cancel flow; browser labels and browser field metrics differ |
| Delete confirmation | [native](images/calendar-api14-delete.png) | Background interaction blocked, explicit Cancel/Delete |
| View presentation | [native renderer](images/calendar-api14-display.png) | Current page fields reach the separate installed renderer |
| UHD | [native](images/calendar-api14-uhd.png) | Measured 2× uniform canvas |
| DCI 4K | [native](images/calendar-api14-dci.png) | Measured 2× with x=128 offset |

The preview uses browser fonts, localStorage and a deterministic September 6
fixture. It does not reproduce the platform keyboard, alarms or Action backend.
Its compact reminder subflow is a browser demonstration; native reminder behavior
is verified through the installed app and Action tests. Focus keyboard mapping
and text metrics differ from NUI; no pixel-identical or browser-as-native claim is
made. Native captures are the acceptance evidence for the installed application.

## Capture provenance and limits

- App: `org.tizen.actionexamples.calendar`, API 14 TPK.
- FHD target: emulator-26111 / tc-0905-actionagent, Tizen 10.1 Unified
  20260905.101134, Common x86_64.
- UHD/DCI: isolated provider-resolution-0906 / emulator-26101, same base image.
- Aurum repository wrapper: remote keys, native coordinate clicks and screenshots.
  Tree returned root_count=0; controls were located from measured ViewAnnotations
  and calibrated screenshots. Lower-right Back/Home belongs to the emulator.
- Fixtures were created through public Actions or actual UI keyboard input,
  queried through Search/resolvers and deleted through public paths after capture.
- Native PNG dimensions were decoded and verified; images were not resized to
  manufacture higher-resolution evidence.
- 8K host scale/inset cases pass. Native 8K is **Unverified on target due to
  emulator DRM constraint**; see the accepted limitation in the stage 1 record.
- Physical TV remote, hardware touch and TV/product profile were not tested.
