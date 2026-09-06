# PhotoGallery stage 2 validation — 2026-09-06/07

2단계 구현·검증 결과입니다. FHD 전체 입력/Action/Presentation 검증과 UHD/DCI 4K
실측을 통과했습니다. 8K는 호스트 TDD만 PASS이며 타깃 검증은 아래 제약으로 미수행입니다.

## Independent acceptance gates

| Layer | Result | Evidence / scope |
|---|---|---|
| Host tests | PASS, five projects | Domain, UseCases, Persistence, App, ActionProvider; `../test.sh` |
| Generated contracts | PASS, three byte-equal files | Fresh complete Photo 8, Custom 2, View 4 generation; no manual source edits |
| Release build | PASS, 0 warnings / 0 errors | Six .NET projects, public `Tizen.NET 14.0.0.19326` |
| TPK | PASS | Manifest equality, executable payload, ZIP integrity, author/distributor signatures, successful install/update on both targets |
| FHD standard/Custom Actions | PASS, 110 checks | `verify_target_actions.py --keep-fixtures`; successful and bounded failing invocations plus mutation postconditions |
| Installed FHD regression | PASS, 109 scenario checks | Release TPK; non-retaining mode, then eight successful cleanup deletions (117 records in temporary report) |
| Final package resource cleanup regression | PASS, 10 checks | Final TPK after JsonDocument disposal cleanup; real Add/Search/Show/GetCurrent/View discovery/Find/ToPresentation/Delete/postcondition |
| FHD UI / View / Display | PASS, 127 checks | `verify_target_views.py`; real native input, every View method, three actual Presentation_Show round trips |
| Additional FHD lifecycle | PASS | Typed UI import, confirmed UI delete, source preservation, restart IDs/favorite, corrupt metadata error and recovery |
| UHD Actions | PASS, 110 checks | Separate disposable VM, real MediaContent and same Action scenarios |
| UHD native rendering | PASS | Actual 3840×2160 capture, uniform 2× canvas and measured View bounds |
| DCI 4K geometry / View | PASS, 81 checks | Actual 4096×2160, 17 published views, FindById/bounds/ToPresentation, 20 complete publication frames, missing/forged failures |
| DCI detail | PASS | Actual Show → GetCurrent ID match → photo View_ToPresentation; 3280×1380 measured image area and native capture |
| Browser preview | PASS, 16 checks | Pictures, D-pad, detail/info/delete, Albums/Favorites, search/import errors, loading/unavailable/empty, UHD/DCI/8K canvas |
| 8K native target | UNVERIFIED | **Unverified on target due to emulator DRM constraint** |

Counts are assertions/invocations/postconditions within each script, not a claim of
110 distinct Action methods. The non-retaining run omits the re-import used
by the 110-check screenshot-fixture run. Cleanup calls are recorded separately.

## Runtime and package provenance

- App/package: `org.tizen.photogallery`, version `0.1.0`, Common profile, .NET API14.
- Source reference: tizenfx Gerrit `tizen_10.1`, commit
  `6837507e05e22a4f5f4e76f532bd42b583d26709`, fetched/checked 2026-09-06.
- Platform contracts: sibling `appfw/tizen-action/default-actions`, complete
  standard categories; custom schemas are in `../actions/`. Platform schemas unchanged.
- FHD target: `emulator-26111` / `tc-0905-actionagent`.
- UHD/DCI target: `emulator-26101` / owned `gallery-resolution-0906` VM.
- Reference image: Tizen 10.1 Unified Common Emulator
  `20260905.101134_tizen-headed-emulator64-wayland`, x86_64.
- Disposable VM runtime preparation used the existing Action 1.3.27 build RPMs and
  matching `provider-action-tool`. This is target setup, not an application Interop
  implementation. Existing `DisplayPresentation` was installed solely as the consumer.
- Aurum bootstrap 1.0.14, repository `aurum-ui` wrapper, native screenshot RPC;
  scoped host ports 55061 and 55062. High-resolution raw reception used
  `TIZEN_AURUM_MAX_MESSAGE_MIB=160`. DCI raw frame: 35,389,440 bytes.
- TPK uses the Tizen Studio **Common Emulator test signer**, not a production
  certificate. Final inspected/installed TPK SHA-256:
  `388a91bd00cc6a98843101e1d1e2a5fced0f6b78bef2cb67d328fe29429a2f9b`.
  Package output is ignored and excluded from the commit.

## Action, state and privacy evidence

All eight standard Photo and two Custom methods have successful and meaningful
failing target cases. The [contract matrix](DEVELOPMENT_GUIDE_Eng.md#action-and-agent-contracts)
records each intent, failure and required follow-up. Input root shape comes from
actual generated dispatch and action-tool execution, including nested Photo.File.

The test imports eight original PNGs through AddPhoto. MediaContent assigns real
IDs; Search and the ordered resolver verify identity, duplicates, unresolved IDs,
limits and current state. Favorite changes are checked in Search/resolver output.
Show/GetCurrent and timer-driven slideshow advancement/stopping are observed.
Delete is followed by Search absence/resolver unresolved, while the original
source file's existence and hash remain unchanged. Non-owned/missing deletion is
rejected. No private/system files are treated as editable Gallery resources.

FHD UI Import typed an actual local path using Aurum keyboard input, confirmed the
copy, and discovered the new ID via Search. UI Delete confirmed that copy's removal
and checked Search absence/source preservation. The path draft was absent from
published annotation context. Restart preserved all eight retained IDs and the
favorite state. A backed-up app metadata file was intentionally corrupted for the
error gate, then restored; typed failure/unavailable UI and subsequent ID/favorite
recovery were independently checked. No corrupted state was left on the target.

## ViewAnnotation and actual Display round trips

FHD verifies 20 consecutive complete frames, finite positive measured bounds,
FindById consistency, current focused views, missing/oversized/stale IDs, forged
identity rejection, and ignoring forged caller snapshots. Modal publication excludes
its underlay; Back restores the launching control. Paused Gallery returns bounded
failure for annotated/focused discovery. Search IME insets resize the same canvas.

Photo_ToPresentation, photo View_ToPresentation and page View_ToPresentation each
produce current-state legacy A2UI v0.8 and invoke the installed renderer's actual
`Tv_Tizen.Action.Presentation_Show`. Aurum captures prove rendered output and return
to Gallery. Metadata binds title/date/album/favorite/ownership. Paths, location,
notes, raw image bytes and caller-injected metadata are excluded. The corrected
surface ID `photogallery-photo` is covered by a host regression test.

| Producer | Installed consumer capture |
|---|---|
| Photo Action | [Action presentation](images/native-action-presentation-fhd.png) |
| Photo annotation | [Photo View presentation](images/native-view-presentation-fhd.png) |
| Current page | [Page View presentation](images/native-page-presentation-fhd.png) |

This verifies the explicitly named **legacy v0.8 compatibility profile**. It does
not establish canonical A2UI v0.9.1, image-catalog rendering, transparent native
windows, compositor alpha or input pass-through.

## Native resolution measurements

SystemInfo reported 1280×720 on the reference image even when the actual Window
was larger. The public WindowSize/GetInsets drawable area therefore controls the
single ancestor transform; SystemInfo capability is recorded rather than blindly
used as the render size. Typography, radii, borders, photos, controls and modal
geometry remain in reference units below that ancestor.

| Viewport | Canvas scale | Measured canvas x/y/width/height | Evidence |
|---|---|---|---|
| FHD 1920×1080 | 1.0 | 0 / 0 / 1920 / 1080 | [Pictures](images/native-pictures-fhd.png), [IME inset](images/native-search-keyboard-fhd.png) |
| UHD 3840×2160 | 2.0 | 0 / 0 / 3840 / 2160 | [Native UHD](images/native-pictures-uhd.png) |
| DCI 4096×2160 | 2.0 | 128 / 0 / 3840 / 2160 | [Native DCI Pictures](images/native-pictures-dci4k.png), [Detail](images/native-detail-dci4k.png) |
| 8K 7680×4320 | 4.0 host expectation | 0 / 0 / 7680 / 4320 **host only** | Tizen-free viewport TDD; no native capture |

DCI first card measured x=256, y=556, width=866, height=568. Detail image actor
measured x=408, y=300, width=3280, height=1380. These are actual View measurements,
not inferred screenshot resizing. DCI reboot let MediaContent index the eight
original source fixtures as well as the eight imported copies, so its Pictures
capture correctly shows 16 records and two pages. The underlying imported photos
remain independently owned; app deletion never targets those originals.

The high-resolution VM intermittently showed a system warning because its Flutter
Home process aborted. The warning could take keyboard focus. Aurum screenshot RPC
also intermittently failed in bootstrap/libdbus; reboot restored native capture.
Experiments pausing the owned VM's starter were undone before the accepted DCI run.
Accepted images were captured after dismissing the Home warning, without modifying
Gallery or compositing the image. The system Back/Home overlay remains visible.
The DCI geometry script checks focus-result consistency, including typed no-focus
when the system owns focus. It is **not** a second complete high-resolution D-pad
acceptance run; positive navigation/trapping/restoration proof is the FHD suite.

8K has no working 7680×4320 DRM output mode on this reference emulator (`e_output
 tmode fail`, established during stage 1 and accepted by the user). **Unverified
on target due to emulator DRM constraint**. Host TDD additionally covers 1280×720,
4:3 letterboxing, ultrawide, asymmetric insets and invalid geometry. Host/browser
results never substitute for native graphics-driver acceptance.

## Retained evidence and cleanup

[UI_PARITY.md](UI_PARITY.md) maps executable HTML and installed NUI primary states.
All selected screenshots are decoded PNGs at their stated native/browser sizes.
They contain original synthetic landscape fixtures; no personal photo library or
proprietary Samsung assets are committed. Historical August HTML-only captures are
retained as pre-existing records but are not used as current acceptance evidence.

The disposable resolution VM and its port 55062 were removed after DCI capture.
FHD test-owned imported IDs were deleted through public Actions with Search absence
checks; this run's UUID source directory and manual source files were removed.
The FHD VM, other applications and unrelated forwards were preserved. Port 55061
was removed without stopping the shared bootstrap. Temporary RPC reports, raw
logs, build directories, generated caches and TPK files remain outside the commit.

Stage 2 commit scope is `PhotoGallery/` plus its artifact exclusions in `.gitignore`.
Concurrent DisplayPresentation, dashboard and root graph output changes are outside
this work and are not included. A clean stage-2 scope does not imply that the
shared repository's unrelated working changes disappear.

TV/product profile, physical USB, physical touch, cloud libraries, Samsung Trash,
and native 8K are not verified by these Common Emulator results.
