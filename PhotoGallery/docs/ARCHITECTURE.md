# PhotoGallery architecture

The API14 implementation supersedes the August scaffold and old Photo ABI.
See [current boundaries and contracts](DEVELOPMENT_GUIDE_Eng.md#boundaries) and
[approved alternatives](STAGE2_PLAN.md). Generated code is immutable; there is no
permission to patch serialization, privilege handling or generated method order.

NUI and typed providers → shared GalleryLibraryService → PhotoRecord/resolver.
MediaContentPhotoLibrary supplies real storage/MediaContent operations; atomic
PhotoMetadataStore owns favorite/title/ownership state. CurrentViewStore publishes
measured immutable frames. PhotoPresentation uses current state and a named legacy
A2UI v0.8 profile. Platform-facing projects remain separate from host domain tests.
