# PhotoGallery development guide

[한국어](DEVELOPMENT_GUIDE.md) · [Documentation index](README_Eng.md)

## Product and public runtime

Samsung Gallery is the primary reference. The official
[Samsung Gallery guide](https://www.samsung.com/us/support/answer/ANS10002535/)
was inspected on 2026-09-06 for Pictures, Albums, search, favorite, information,
delete and slideshow. Its exact binary version is unspecified and behavior can
vary by device. The 1920×1080 layout, 4×2 paging and D-pad order are a Tizen
adaptation. No Samsung proprietary artwork or Music branding/assets are copied.
See [approved design and alternatives](STAGE2_PLAN.md).

`Tizen.NET 14.0.0.19326` references public API14. The source reference is Gerrit
`tizenfx/tizen_10.1` revision `6837507e05e22a4f5f4e76f532bd42b583d26709`
(Release 14.0.0.19360), fetched and checked on 2026-09-06.
The manifest uses Common OS profile 10.0 with .NET application API version 14.
There is no native PhotoGallery implementation or handwritten Interop.

`StorageManager.Storages` determines internal and USB roots. MediaDatabase and
MediaInfoCommand read real registered images and register imported copies.
The app does not scrape folders into synthetic photo IDs. MediaContent IDs stay
stable across queries, favorite changes, viewing and restarts. A new import is
a new copy and receives a new ID. SD-card/network roots that cannot be represented
by the current File.StorageType contract are excluded. Physical USB is unverified.

Import accepts an absolute JPEG/PNG path below a supported storage root, at most
32 MiB. It rejects symbolic path components and non-image headers, copies to
`Images/PhotoGallery/<random-name>`, registers through public MediaInfoCommand.Add,
and persists ownership/title metadata. Source files are never modified.
Only registered, path-matching owned copies can be deleted. A hidden backup is
kept until MediaContent acknowledges deletion; failure restores that copy.
Favorite is app metadata because MediaInfoCommand.UpdateFavorite is deprecated
since API12. Atomic JSON replacement publishes memory only after the durable
write. On partial platform failure, the use case reconciles its cache with the
actual library before returning a failure, so follow-up queries report durable state.

Metadata reads are bounded to 4 MiB; adding new metadata keys is capped at
5,000 entries. Library reads inspect at
most 20,000 records and return at most 5,000 image records. Media errors and corrupt
metadata reach the UI's unavailable state. Corrupt metadata is preserved for
recovery; restoring valid metadata and restarting restores the library.
No personal photos, imported files, build output, private metadata or RPC logs are
committed. `tests/fixtures` holds eight original test-only landscape PNGs.

## Boundaries

- Domain: photo records, bounds, ordered resolver and existing pure interaction rules.
- UseCases: shared GalleryLibraryService, serialized refresh/mutations, current
  viewer/slideshow state and current-state presentation builder. The existing
  query/refresh cancellation tests remain host-runnable.
- Persistence: public MediaContent adapter and atomic app-data metadata store.
- ActionProvider: complete generated Photo/PhotoGalleryCustom stubs with thin
  ServiceBase subclasses; no edits to generated code.
- ViewActionProvider: complete generated View category, atomic current-view store,
  identity validation and live View-to-Presentation conversion.
- App: NUI composition, actual images, keyboard/pointer/D-pad, focus and measured
  annotation publication. UI and providers share one service without self-RPC.

Generation uses the authoritative `../appfw/tizen-action/default-actions` catalog
relative to this repository's parent. `generate-bindings.py` generates each whole
category. Custom v1 slots use alphabetical fallback: GetPhotoByIds, SetFavorite.
Never insert/reorder an existing Custom ABI slot or patch generated C#; change
schemas and regenerate a versioned category when necessary. Generated whitespace
is preserved even when a formatter would remove it.

## Action and Agent contracts

Single Entity inputs are the root `arguments` object: no `photo`, `query`, or
`presentation` envelope. Object-schema inputs such as `ids` retain their named
properties. Entity `ToJson()` annotation snapshots have generated type wrappers;
RPC JSON returned by action-tool is a different projection.

Example `action-tool execute --json` request:

```json
{"id":1,"params":{"name":"Tv_Tizen.Action.Photo_AddPhoto","appid":"org.tizen.photogallery","arguments":{"Id":"","Extra":"Lake","File":{"Path":"/opt/usr/home/owner/media/Images/lake.png","StorageType":"internal","MimeType":"image/png"}}}}
```

Returned Photo contains Id, versioned JSON Extra (`title`, `album`, `favorite`,
`owned`), Date and nested File. File carries Id, Path, StorageType, Size,
ModifiedDate and MimeType. Add accepts Extra as an optional title ≤200 characters;
caller-supplied Photo.Id must be empty. Other photo mutations and presentations
resolve Id against current state and do not trust caller snapshots.

Search: Id ≤256, Keyword ≤256 (title/album/date), Category empty or `Photo`,
`Tizen.Entity.Photo`, `org.tizen.photogallery`; Limit 0 means 100, otherwise 1..200.
ID filtering occurs before limiting. Custom resolver takes 1..100 nonempty IDs
≤256, preserves order and duplicate matches, and reports unresolvedIds explicitly.
Status is the schema's typed Success/Reason pair; unsuccessful calls provide a
bounded validation/missing/unavailable/internal reason and initialized output DTOs.

| Action suffix (standard Photo unless qualified) | Agent intent / success | Meaningful failure | Follow-up / visible effect |
|---|---|---|---|
| AddPhoto | Import a local image copy | Missing/unsupported/out-of-root path, caller ID | Search discovers new MediaContent ID |
| DeletePhoto | Remove an owned copy | Missing ID or non-owned photo | Search empty; resolver reports unresolved |
| GetCurrent | Identify displayed photo | No active viewer | Returns Show/slideshow selection |
| Search | Discover by ID/name/album/date | Oversized keyword, invalid limit/category | Stable current Photo/File entities |
| Show | Inspect a discovered photo | Missing ID | Detail image; GetCurrent matches |
| StartSlideshow | Browse actual library images | Empty library or already running | GetCurrent advances after timer |
| StopSlideshow | Retain current photo | No running slideshow | GetCurrent remains unchanged |
| ToPresentation | Display current photo metadata | Missing ID | Presentation_Show displays current state |
| PhotoGalleryCustom_GetPhotoByIds | Resolve stable context in request order | Empty/oversized ID list | Ordered duplicates and unresolved IDs |
| PhotoGalleryCustom_SetFavorite | Remember a photo | Missing/invalid ID | Search/resolver Extra.favorite; Favorites tab |
| View_GetAnnotatedViews | Discover currently rendered context | Paused/no active annotated surface | Measured page/control/photo views |
| View_GetFocusedView | Identify focus for the next input | No current annotated focus / paused | Actual input/navigation focus |
| View_FindById | Revalidate a view before acting | Missing/stale/oversized view ID | Current snapshot and bounds |
| View_ToPresentation | Explain the current screen or photo | Removed view / mismatched entity identity | Live snapshot → Presentation_Show |

Standard names start `Tv_Tizen.Action.Photo_`, Custom names
`App_Tizen.Action.PhotoGalleryCustom_`, and View names `Common_Tizen.Action.View_`.
Custom extensions exist because standard Photo has neither an ordered batch
resolver with unresolved IDs nor a favorite mutation. Platform schemas are unchanged.

Example Agent task: “Find Lake and mark it as a favorite” → Photo_Search Keyword
`Lake` → choose stable Photo.Id/File context → Custom_SetFavorite → Search by Id
and observe favorite=true. “Describe the photo I am looking at” → View discovery
or Photo_GetCurrent → current Photo/Annotation → ToPresentation → Presentation_Show.
“Delete this imported photo” → revalidate Id/owned → DeletePhoto → Search/resolver
absence. UI confirmation protects direct user deletion; explicit Action deletion
is an already requested command.

## Rendering, scaling and lifecycle

Geometry and PixelSize in App are **reference design units**, not physical screen
pixels. SystemInfo `Information.TryGetValue(screen.width/height)` records screen
capability at initialization. `WindowSize` and `GetInsets()` determine the real
drawable area. Some Common Emulator profiles report stale 1280×720 SystemInfo;
it does not override the actual window. One centered uniform ancestor transform
scales all child layout, image areas, typography, radii, borders and focus geometry
once. Root background remains full-window. Invalid/nonfinite window/insets leave
the previous valid root intact. IME insets resize the same canvas without discarding
text/focus. FHD=1×, UHD=2×, DCI 4K=2× with 128-pixel physical side margins, 8K=4×.

Startup loading focuses Import; that focus is retained when the scan completes.
When no previous focus remains, the first available photo (or Import in an empty
library) receives focus. Pictures use four columns and eight items per page.
D-pad steps through columns/rows and surrounding controls; Enter activates.
Viewer Back restores the source photo. Delete/Info/Import trap focus; Cancel/Close
is initial, Back restores the launching control. Search separates the draft from
the applied query; Apply hides the IME and restores its button. Import drafts are
redacted from ViewAnnotation. Actual pointer clicks and emulated keyboard/D-pad
are verified; Aurum's empty accessibility tree uses measured bounds as fallback.

Meaningful page, control and photo annotations use generated Entity.ToJson().
Only finite positive measured CalculateScreenPositionSize bounds are published.
Layout/focus/input changes replace one immutable frame immediately and again after
50 ms/layout stabilization; root removal and pause/terminate clear current views.
Modal snapshots exclude underlying controls. View_ToPresentation matches live
View.Id + EntityType + EntityId and ignores caller EntityInfo. Deleted/surface-stale
IDs and forged identities fail.

Presentation uses the explicitly named **legacy A2UI v0.8** split surfaceUpdate
Template / dataModelUpdate Document profile accepted by the installed renderer.
Text/Column components bind current title/date/album/favorite/ownership; surface ID
`photogallery-photo` obeys the renderer's identifier profile. Paths, location, notes,
raw image bytes and arbitrary styles are excluded. This is metadata presentation;
photos themselves render natively in Gallery. Canonical v0.9.1, transparency and
Image catalog support are not claimed. Action-to-display, photo View-to-display,
and page View-to-display are separate target checks.

## Reproduction

```sh
cd PhotoGallery
./test.sh
./build.sh
./package.sh
python3 generate-bindings.py --output-root /tmp/gallery-generated-check
# Compare each generated *.cs byte-for-byte before packaging.
sdb -s emulator-26111 install dist/org.tizen.photogallery-0.1.0-api14.tpk
../.agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui session-start --serial emulator-26111 --port 55061
python3 tests/verify_target_actions.py --keep-fixtures
python3 tests/verify_target_views.py
```

The Action script creates a UUID source directory and records its own imported IDs
in `/tmp/photogallery-actions.json`; without --keep-fixtures it removes only those
copies/source fixtures. Keep fixtures only while doing the following UI acceptance,
then delete the recorded owned IDs through Actions and remove that run's UUID
source directory. View script requires an installed DisplayPresentation and fixture
favorites. Dismiss the Common Emulator's missing-home warning before native input
verification; it is a platform popup, not Gallery UI. Screenshots and temporary
RPC reports go to /tmp unless deliberately selected for docs/images.

See [validation](STAGE2_VALIDATION.md) and [HTML/native parity](UI_PARITY.md).
8K: **Unverified on target due to emulator DRM constraint**. Host geometry passes;
there is no supported 7680×4320 DRM output mode in this reference emulator. Product
TV, real USB and physical touch gates remain distinct from Common Emulator evidence.

## Screenshot evidence and provenance

| Pictures | Detail |
|---|---|
| ![Pictures](images/native-pictures-fhd.png) | ![Detail](images/native-detail-fhd.png) |

2026-09-06/07, Public Tizen 10.1 Unified Common Emulator,
`emulator-26111` FHD / `emulator-26101` UHD/DCI, app `org.tizen.photogallery`.
Aurum remote-key, native-coordinate input and screenshot RPCs were used. The
accessibility tree was empty; measured View bounds and inspected native frames
provided the fallback. Original PNG fixtures were imported through public AddPhoto.
The system Back/Home overlay remains visible. Native dimensions and the complete
state-by-state comparison are recorded in UI_PARITY and the validation document.
