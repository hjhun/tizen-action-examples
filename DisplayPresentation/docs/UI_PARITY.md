# DisplayPresentation UI parity

Profile: [A2UI and One UI](A2UI_ONE_UI_PROFILE.md).
Single executable preview: [one-ui-sample.html](../refs/one-ui-sample.html).

## 2026-09-06 evidence

The preview fixtures are generated from actual Calendar domain producers, not
handwritten renderer-only data. Regenerate them from the repository root:

```bash
dotnet run --project DisplayPresentation/tests/DisplayPresentation.UseCases.Tests -c Release -- --write-fixtures DisplayPresentation/refs/fixtures.js
python3 DisplayPresentation/tests/check_preview.py --browser-path /path/to/chrome --screenshots DisplayPresentation/docs/images/browser
```

The fixture selector is visible only with `?verify=1`, outside the product canvas.
The regular preview shows the same renderer-owned page navigation as NUI.

| State/check | Browser result | Installed NUI result |
|---|---|---|
| Calendar 0/1/7/100 | Pass; empty placeholder / 4 / 28 / 400 fields; all 100 pages reached | Pending |
| Reminder | Pass; four actual producer fields | Pending |
| Page 2, Next focused | [FHD browser capture](images/browser/presentation-page-2-fhd.png) | Pending |
| Previous/Next/Enter | Pass; page transition and focused Next | Pending |
| 720p, FHD, 4K, 8K, 4:3 | Pass; centered canvas fits viewport and preserves page | Pending native insets/scaling/bounds |
| Escape/dismiss | Pass; neutral empty state, no old content | Pending |
| Malformed/unsupported | Pass; recovery surface, no untrusted partial rendering | Pending |
| Browser console | No errors recorded | Not applicable |
| ViewAnnotation page/control lifecycle | Host paging/store checks pass | Pending measured geometry, focus and actual RPC |

![Browser preview, Calendar page 2 with Next focused](images/browser/presentation-page-2-fhd.png)

This image is Chromium evidence, not an Aurum/NUI screenshot. The HTML uses the
available host font stack; exact native text metrics and control visuals are not
yet compared. Native captures must be stored separately and labeled with target,
build, viewport, input and state. Loading, payload Button/TextField and native
overlay composition remain unimplemented; renderer navigation does not establish
payload Button support.

## Cross-app flow ledger

| Source | Current evidence | Remaining gate |
|---|---|---|
| Calendar event/reminder ToPresentation | Real host builders parse; generated browser fixtures pass | Installed Action output → Show |
| Calendar/Reminder page/control snapshot | Shared builder parses and round-trips in host tests | Installed View discovery → ToPresentation → Show |
| DisplayPresentation visible page | Host page serialization; [one FHD PhotoGallery case](#photogallery-native-pairing-2026-09-08) verifies page replacement, stale View rejection and current-page View → Show | Other payloads, paging/input/lifecycle cases and resolutions remain open |
| Browser | Current production legacy builder → parser → page serialization passes in the 2026-09-08 host follow-up below | Fresh installed producer/renderer payload pairing and native acceptance; host result is not canonical support |
| PhotoGallery | Five-field host coverage; [one installed FHD pairing](#photogallery-native-pairing-2026-09-08) verifies producer Action → two renderer pages → current renderer View → Show | PhotoGallery source View round trip and broader native acceptance remain open |

Follow [target validation](TARGET_VALIDATION.md) to close each installed gate.
Compare geometry, spacing, type, colors, states, labels, focus, density and every
supported input mode. A successful build, package or browser run is not native
parity or product completion.

## 2026-09-08 source review

The previously local API14/legacy-binding/paging implementation is now included
in the source delivery. Fresh actionc generation of the complete Presentation and
View categories matches both files byte-for-byte; the five advertised actions and
positional slots match the local catalog. Generated whitespace is preserved as
raw output; authored changes pass whitespace checks.

Domain, Persistence and UseCases host suites pass, including actual Calendar
producer compatibility, bounded binding rejection and page serialization tests.
Release build completes with zero warnings/errors. These are portable/compile
checks, not host execution of managed RPC providers. Production source is unchanged
from the five-app package preparation in [Packages](../../Packages/README.md).
The browser capture and matrices above retain their 2026-09-06 scope; no new
browser, target installation or native acceptance was performed in this review.
Canonical A2UI, overlay hosting and unexercised input/lifecycle cases remain open.

## Browser / PhotoGallery producer regression (2026-09-08)

The UseCases suite references the actual portable Browser and PhotoGallery use-case
projects; it does not load their NUI or generated provider assemblies. Its 54 new
checks pass with unchanged production code, filling the previously missing host
integration coverage rather than reproducing and fixing a production failure.

- Browser: field order, bounded title/details, query/fragment exclusion and
  semantic page serialization preserve values, IDs and roles. Supplying the
  producer's canonical lifecycle messages to the legacy parser is rejected;
  this rejection test does not validate canonical protocol conformance.
- PhotoGallery: both favorite and ownership states preserve all five fields over
  two pages. Serialized page data contains only visible field IDs, and the parser
  reconstructs the same values and order. File path, location and private note
  are absent. A numeric title substituted into the real producer document fails
  with portable `InvalidInput`; this is not a typed target RPC failure test.

The existing Calendar/shared producer, binding rejection and paging tests also
pass. No actual Action calls, installation, native page/focus/annotation inspection
or new UI captures were performed in this follow-up. Earlier target evidence keeps
its original payload and scenario scope.

## PhotoGallery native pairing (2026-09-08)

One new, non-sensitive Photo fixture passed on `tc-0905-actionagent` / `emulator-26101`
using the legacy v0.8 compatibility profile. Exact [Packages](../../Packages/README.md)
artifacts (source `ff7a432`, package commit `456996e`):

- PhotoGallery TPK SHA256: `f185325c0de37a2a4e7e712aea8f7f4147408124b25d3ba61cafd518cda45199`.
- DisplayPresentation TPK SHA256: `60bae90956edfb4bae9c8df2cc71c105302c5745a3e71eacedadf9d756f7313b`.

The actual Photo_ToPresentation output was passed unchanged to renderer Show.
Three original 1920×1080 Aurum frames show the first four values on page 1/2
(Next focused), ownership alone on page 2/2 (Previous focused), and the current
renderer View's returned Presentation shown as one page (Dismiss focused).
After Next was clicked once and page 2 was published, FindById for the old
first-page View (zero-based `page:0` ID) failed
with a non-null empty View, bounds and Annotation; old View_ToPresentation failed
with empty Template/Document. Current page 2 succeeded and its serialized data
contained ownership only. The 23 actual Action calls comprise 21 typed successes
and these two expected stale failures. Measured View bounds fit each native frame.

Restricted host evidence: `/tmp/p5-gallery-renderer-execution/` contains
`final-report.json`, `wire-typed.jsonl`, `roundtrip-result.json`, `page1.png`,
`page2.png`, `roundtrip-one-page.png` and `final-cleanup-result.json`; these temporary
artifacts are not committed. This native case is separate from the 54 host checks
above and does not verify PhotoGallery's source View round trip, canonical A2UI,
overlay, all paging/input cases or other resolutions. Historical Pending entries
retain their original scope.

The one imported fixture was deleted and Search/resolver confirmed absence; its
source PNG remains under `media/Images/p5-renderer-332a1f2e49b54bb6a00108ecb1e4ef15/`.
Existing business-file hash was unchanged. PhotoGallery returned to stopped and
renderer to running with test content dismissed; own forward/wire files were
removed and owner restored. PID/starttime matched at the 05:44:56–05:47:28 UTC
observations, with no new dump in that interval. Renderer memory-session loss was
explicitly accepted; signed rollback and complete security-state restoration were
not established. The preceding authored parent/child directory-check failure
remains separately recorded in `prepare-result.json`, not counted as a native pass.
