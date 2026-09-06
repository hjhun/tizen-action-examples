# PhotoGallery

A .NET API 14 Tizen Action provider adapting Samsung Gallery's Pictures, Albums,
and detail flows to Tizen NUI and remote input. App/package ID: `org.tizen.photogallery`.

Browse real MediaContent images, import JPEG/PNG copies, persist favorites, search,
inspect details, and run a slideshow. Delete confirmation removes only copies
imported by this app; source images and other device photos are preserved.

![Pictures](docs/images/native-pictures-fhd.png)

| Detail | Delete confirmation |
|---|---|
| ![Detail](docs/images/native-detail-fhd.png) | ![Delete](docs/images/native-delete-fhd.png) |

These are installed Common Emulator screenshots captured through Aurum. Original
local test PNGs were imported through public Actions; no fixture library ships in
the product. Bottom-right Back/Home chrome belongs to the emulator.

Captured 2026-09-06/07 on Tizen 10.1 Unified Common Emulator: `emulator-26111` FHD (1920×1080), `emulator-26101` UHD/DCI 4K.
Repository Aurum key/coordinate/screenshot RPCs used measured View bounds when
the accessibility tree was empty. SystemInfo and WindowSize/GetInsets determine
one ancestor transform shared by pages and modals.

```sh
./test.sh             # Five host test projects
./build.sh            # Release .NET/API14 build
./build.sh generate   # Complete Photo 8 / Custom 2 / View 4 regeneration
./package.sh          # Emulator-signed TPK under ignored dist/
```

[Development guide](docs/DEVELOPMENT_GUIDE_Eng.md) ·
[Validation evidence](docs/STAGE2_VALIDATION.md) · [HTML/NUI parity](docs/UI_PARITY.md) ·
[Executable preview](refs/one-ui-sample.html) · [한국어](README.md)

8K host geometry TDD passes. **Unverified on target due to emulator DRM constraint**.
Common Emulator validation does not establish TV-product, physical USB or touch acceptance.
