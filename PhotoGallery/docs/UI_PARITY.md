# PhotoGallery HTML / NUI parity — 2026-09-07

The canonical [executable sample](../refs/one-ui-sample.html) and installed .NET
Gallery implement Pictures, Albums, Favorites, search, detail, info, slideshow,
import and deletion confirmation. This ledger supersedes the August browser-only
proposal. Requirements and architecture are in Markdown, not embedded in the sample.

## Screen metadata failure isolation (2026-09-08)

`ReadDisplay` now isolates optional SystemInfo screen metadata from drawable
measurement. A false result, exception or non-positive value on either axis resets
both metadata dimensions to zero; valid WindowSize/GetInsets can still determine
the viewport. Invalid drawable geometry retains the existing false/old-frame policy.
`PhotoGallery.App.Tests` passes and the Release build has zero warnings and errors.
These were regression/compile results: the suite at that stage did not execute Information
exceptions or directly test a valid window with unavailable screen metadata.
Native display after SystemInfo exceptions or partial reads remains unverified,
and the SystemInfo initial sizing requirement remains unmet. Earlier FHD/UHD/DCI
evidence for `org.tizen.photogallery` retains its original payload/date scope and
does not validate this change on target.

## Optional screen metadata host coverage (2026-09-08)

The App suite now directly exercises the production `GalleryDisplayMetrics` seam
with valid FHD/UHD/DCI 4K/8K windows and asymmetric insets: absent, partial and
negative screen metadata normalize to zero without changing drawable geometry.
A 1280×720 screen capability also leaves each actual window's geometry unchanged;
missing metadata does not validate a drawable exhausted by insets. All 76 added
assertions and the existing suite pass without a production change. This fills the
portable coverage gap above, not a reproduced production failure or TDD RED/GREEN.
Actual Information exceptions, native partial-read rendering and SystemInfo
initial sizing remain unverified/unmet as previously recorded.

## Reference and adaptation

Primary reference: Samsung Gallery; official [Gallery support guide](https://www.samsung.com/us/support/answer/ANS10002535/),
inspected 2026-09-06 for Pictures/Albums, search, information, favorite, delete and
slideshow. The guide does not identify a precise binary version and allows device
variation. Four-column paging, TV-distance type, surrounding remote controls and
explicit focus restoration are Tizen adaptations. Deletion applies only to
app-imported copies and does not claim Samsung Trash retention or cloud features.

Both surfaces use one centered 1920×1080 canvas, a quiet light library, dark image
viewer, rounded controls, restrained blue focus and a bounded modal. Native NUI
uses actual MediaContent; the browser uses local original fixture PNGs. The browser
`galleryPreview.library(...)` seam loads the native fixture ordering/metadata for
comparison, and `state(...)` exposes test-only loading/error states. These seams
are not product commands. The TPK includes no fixture photo library.

## Captured state comparison

Every link below points to retained evidence. All paired images are 1920×1080.
Hierarchy, geometry, spacing, content density, colors, controls, state, focus and
scaling were compared. Native type uses the platform font; browser uses Arial.

| State | Browser | Installed NUI | Comparison / disposition |
|---|---|---|---|
| Pictures | [Preview](images/preview-pictures-fhd.png) | [Native](images/native-pictures-fhd.png) | Same 4×2 grid, 433×230 thumbnails, captions, header commands and bottom tabs. Preview thumbnail fitting and tab widths were aligned to native. Native startup retains Import focus from loading. |
| D-pad photo focus | [Preview](images/preview-dpad-focus-fhd.png) | [Native](images/native-dpad-focus-fhd.png) | Both expose visible blue focus plus a second border/scale cue and bounded traversal. Different tested starting selections are intentional. |
| Albums | [Preview](images/preview-albums-fhd.png) | [Native](images/native-albums-fhd.png) | Folder-derived cover and name lead to album pictures; test imports form Gallery album. |
| Album pictures | [Preview](images/preview-album-pictures-fhd.png) | [Native](images/native-album-pictures-fhd.png) | Same page/grid under selected album heading; Back returns to album discovery. |
| Favorites | [Preview](images/preview-favorites-fhd.png) | [Native](images/native-favorites-fhd.png) | Same persisted favorite subset. Native heart uses platform emoji coloring; browser glyph is monochrome. |
| Detail | [Preview](images/preview-detail-fhd.png) | [Native](images/native-detail-fhd.png) | Full image aspect is preserved in the 1640×690 actor, with Back/title and six commands. Native decode sampling and font rasterization differ. |
| Information | [Preview](images/preview-info-fhd.png) | [Native](images/native-info-fhd.png) | Same title/album/date/owned-copy context and bounded close control. Metadata comes from current state, not caller snapshots. |
| Delete | [Preview](images/preview-delete-fhd.png) | [Native](images/native-delete-fhd.png) | Same modal and irreversible-copy wording. Browser capture is after Right to confirm; native capture is initial Cancel. Both states and Back restoration are asserted. |
| Applied empty search | [Preview](images/preview-search-empty-fhd.png) | [Native](images/native-search-empty-fhd.png) | Same Search/Close controls and no-results recovery. Native Apply explicitly dismisses the IME and retains button focus. |
| Search input / IME | Browser uses host input field | [Native keyboard](images/native-search-keyboard-fhd.png) | Native system IME owns its inset; the uniform canvas shrinks and centers without discarding draft. Browser has no Tizen IME, an explicit platform difference. |
| Import form | [Preview](images/preview-import-fhd.png) | [Native](images/native-import-fhd.png) | Same local-file workflow and Cancel-first modal. Only native performs real copy/MediaContent registration. |
| Import validation | [Preview](images/preview-import-error-fhd.png) | [Native](images/native-import-error-fhd.png) | Both show bounded invalid-path feedback inside the modal. The browser's fixture-only message is an adaptation; actual storage restrictions are native. |
| Empty library | [Preview](images/preview-empty-fhd.png) | [Native](images/native-empty-fhd.png) | Same no-pictures hierarchy and Import recovery. No invented photo entities. |
| Unavailable / recovery | [Preview](images/preview-error-fhd.png) | [Native error](images/native-metadata-error-fhd.png), [restored](images/native-recovered-fhd.png) | Browser injects state. Native corrupt-metadata test reaches real failure, then valid-state restore/restart recovers IDs and favorite. |
| Loading | [Preview](images/preview-loading-fhd.png) | Transient real MediaContent scan, no retained frame | Native async loading is implemented; a stable captured loading frame is not claimed. Its controls/empty layout share the tested root. |

Native source-typed [Import](images/native-import-typed-fhd.png), its
[result](images/native-import-result-fhd.png), and confirmed
[Delete result](images/native-delete-result-fhd.png) prove the real mutation UI.
Search verifies the new ID and its later absence; the source remains intact.
Slideshow advances actual selected photos on the 3-second timer and Stop retains
selection; these temporal postconditions are Action assertions rather than a still
image pretending to prove movement.

Small font metrics, anti-aliasing, emoji rendering, focus ring rasterization and
the native Back/Home overlay are platform differences. Browser controls cannot
replicate storage, Tizen IME or MediaContent and are not evidence for those APIs.
No screenshots are reconstructed, composited or scaled to claim a native profile.

## Scaling and interoperability

| Slice | Evidence | Scope |
|---|---|---|
| FHD | Paired table above | Full UI, D-pad/pointer/keyboard, modal/back restoration and live annotations |
| UHD | [3840×2160](images/native-pictures-uhd.png) | Actual native 2× rendering, bounds and 110 Action checks |
| DCI 4K | [Pictures](images/native-pictures-dci4k.png), [Detail](images/native-detail-dci4k.png) | Actual 4096×2160, 2× canvas, 128px side margins, 81 geometry/View checks |
| 8K | Host and browser 7680×4320 tests | **Unverified on target due to emulator DRM constraint** |
| Current-state presentations | [Action](images/native-action-presentation-fhd.png), [photo View](images/native-view-presentation-fhd.png), [page View](images/native-page-presentation-fhd.png) | Actual installed DisplayPresentation, legacy A2UI v0.8; renderer UI belongs to DisplayPresentation |

The browser suite has 16 assertions, the full FHD suite 127. High-resolution Home
warnings/occasional Aurum transport crashes required a disposable-VM reboot and
warning dismissal. Complete high-resolution D-pad input is not inferred from
geometry/captures; see [validation](STAGE2_VALIDATION.md) for exact boundaries.

Capture provenance: 2026-09-06/07, Public Tizen 10.1 Unified Common Emulator,
`emulator-26111` FHD and owned `emulator-26101` UHD/DCI, app
`org.tizen.photogallery`, repository Aurum screenshot/remote-key/native-coordinate
RPCs. Aurum accessibility tree was empty; actual View bounds and inspected native
frames supplied the coordinate fallback. Browser captures use Chromium through
agent-browser. Original test PNGs are in `../tests/fixtures/`. Pre-existing August
`html-*.png` captures are historical and not current parity evidence.

## 2026-09-08: Stable delete confirmation target

Opening Delete on photo A now fixes the confirmation target to A's stable ID.
External Show B changes the viewer without rebinding the dialog, Confirm or
annotation context. Metadata for A may refresh; missing or non-owned A fails
explicitly without deleting a replacement. Closing or successfully confirming
clears the pending session; service ownership/path guards remain in force.

Host tests execute `PhotoDeleteConfirmation` with the real
`GalleryLibraryService` and recording media adapter: selection changes, external
deletion, metadata refresh, invalid/non-owned targets and independent sessions.
The initial RED was a missing-type compile failure, not execution of the old UI
bug. UseCases and App suites passed; Release built with zero warnings/errors.

On `tc-0905-actionagent` / `emulator-26101`, `org.tizen.photogallery` 0.1.0,
FHD Common Emulator, actual Action/Aurum checks verified A1 dialog → Show B →
Confirm deletes only A1; external deletion of A2 → Confirm shows an explicit
missing-target error while B survives; Cancel/Info/Import close → new C dialog
uses C. Screenshots, pending annotation IDs/focus and subsequent Search/resolver
results were reviewed. Missing-A Confirm is a **UI failure**, not a typed
DeletePhoto RPC failure test. Busy-frame timing, full UI acceptance and other
resolutions were not verified by this run; historical evidence above retains
its original payload/date scope.

Validated TPK SHA256:
`4943286305d5ea3a572ed8952db8c1ee895b220ca26dd0e8837da8f8852d87b6`.
All four imported fixtures were deleted and their IDs resolved as missing.
The four source PNGs remain under
`/opt/usr/home/owner/media/Images/p5-delete-confirm-b1f27883317b41a3be1e8a7fc769c84b`.
The pre-existing app-owned regular file returned to its original hash; no new
dump was observed. The updated installation remains stopped, with owner mode
and the test forward restored. Exact restoration of the previous signed binary
is not guaranteed. SystemInfo initial sizing, 8K native and other open runtime
and UI gates remain unverified.
