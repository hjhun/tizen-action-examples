# PhotoGallery API 14 / One UI implementation plan

Approved stage 2 scope, 2026-09-06. This record supersedes the August draft's old
Photo ABI and any permission to patch generated sources. Generated output is
immutable; the current whole Photo and View categories are regenerated unchanged.

## Product and architecture

Use `org.tizen.photogallery`, .NET/API 14, Samsung Gallery Pictures-first grid,
Albums (device folders), Favorites, search, image detail/info, slideshow and
Delete confirmation. The existing Tizen-free tests and query cancellation seams
are retained. NUI, MediaContent/persistence, use cases, domain and generated
Action/View providers remain separate projects.

Sources inspected 2026-09-06:
- Samsung official Gallery guide: https://www.samsung.com/us/support/answer/ANS10002535/
  Pictures/Albums, search, favorite, info, delete and slideshow interaction. Exact
  Gallery binary version is not stated; the guide explicitly allows device/version
  variation. Large-window geometry and D-pad adaptation are our implementation.
- Public `tizenfx` tizen_10.1 commit 6837507e05e22a4f5f4e76f532bd42b583d26709.
  MediaInfoCommand.Add registers real image files; Delete requires the underlying
  file to be removed first. UpdateFavorite is deprecated since API 12, so Favorites
  are app-owned persisted metadata keyed by stable MediaContent IDs.

Compare two approaches: a raw app-folder-only catalog is rejected because it
would bypass MediaContent identity and real device photos. Read the real
MediaContent library and import copies into an app-owned album instead. Import
uses managed file I/O followed by public MediaInfoCommand.Add; it never modifies
the source file. Delete applies only to app-owned imported copies. Other device
photos remain viewable and favoritable. This bounds destructive resource ownership
while providing an actual mutable MediaContent library. Ownership/favorite/title
metadata uses atomic app-data JSON; compensated failures remove only the new copy.

## Contracts

Standard Photo has 8 methods: AddPhoto, DeletePhoto, GetCurrent, Search, Show,
StartSlideshow, StopSlideshow, ToPresentation. Photo embeds File, not a legacy Path.
The complete category order comes from default-actions/action.seq.
PhotoGalleryCustom adds only GetPhotoByIds (order/duplicates/unresolved) and
SetFavorite (stable ID + boolean). Extra carries bounded app-owned title/favorite/
album/ownership context. All nested generated DTO fields are initialized.

UI/providers share a synchronized library service and viewer state. Provider
commands return only after their domain operation completes. Media scans/imports
run outside the NUI thread; UI receives immutable snapshots through its captured
SynchronizationContext. Mutations/refreshes serialize; pause and termination clear UI annotations.
The existing standalone query coordinator retains cancellation/stale-result tests. Collection/query/file sizes are bounded; error states offer recovery.

Presentation derives from the resolved current Photo or live View snapshot and
uses the installed renderer's explicitly named legacy A2UI v0.8 compatibility
profile. No canned payload, raw source path, location, image bytes, arbitrary
styling or canonical v0.9.1 claim. Both Action and View round trips are required.

## UI and input

One 1920×1080 reference canvas; public SystemInfo records screen capability,
WindowSize/GetInsets determines the centered uniform ancestor transform.
All child geometry/type/border/radius/focus values are design units. Resize keeps
unsaved input and focus. Only finite positive native View bounds are published.

Pictures and album/favorite results use an 8-photo page with deterministic 4×2
D-pad movement and Previous/Next pagination. Top/bottom controls join the same
focus graph. Enter opens, Back restores the source card. Detail offers previous/
next, favorite, info, slideshow and Delete. Modal Cancel gets initial focus,
left/right stay inside, Back cancels and restores the launching control.
Search/import fields support keyboard, pointer and platform virtual keyboard.

## Verification sequence

1. Browser preview primary/empty/error/loading/confirmation states and scaling.
2. RED→GREEN domain/use-case/ownership persistence tests, resolver/query bounds,
   current viewer/slideshow, cancellation/stale publication and geometry matrix.
3. Complete generation byte equality, API14 build 0 warnings/errors, TPK checks.
4. Installed real MediaContent capability + every standard/Custom/View method's
   successful and meaningful failing invocation; mutation Search/resolver
   postconditions, actual image Show/slideshow/current state and restart persistence.
5. Aurum primary pages, focus, search, import, confirmation/cancel, image detail,
   ViewAnnotation and actual DisplayPresentation round trips; HTML/native parity.
6. FHD/UHD/DCI 4K native evidence. 8K host TDD; stage 1 DRM constraint remains
   explicitly unverified on native target unless a different capable target exists.
7. Bilingual docs, precise staged-file/artifact review, commit/push and final
   [완료] prompt to agy w1:pD after all applicable gates pass.
