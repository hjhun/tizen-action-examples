# PhotoGallery UI parity ledger

> Status: the executable browser sample is implemented and pending its first installed NUI counterpart. This ledger never treats a browser capture as native evidence.

## Reference audit and Tizen adaptation — 2026-08-09

**Primary task and surface:** browse a real device photo library, find a picture, inspect it, and explicitly confirm removal. This is an **Explore** surface: date-grouped image discovery is primary; search and detail are drill-down states.

### Authoritative reference sources inspected

1. Samsung Korea, [Samsung Gallery](https://www.samsung.com/sec/apps/samsung-gallery/) — first-party Samsung Gallery product page; fetched successfully on 2026-08-09.
2. Samsung, [One UI](https://www.samsung.com/us/one-ui/) — Samsung’s first-party One UI entry point; fetched on 2026-08-09 (the regional endpoint redirected to Samsung US home in this environment).
3. Samsung Support URL research was attempted on 2026-08-09. The older US support route redirected to the generic phones category and the public Galaxy Store detail endpoint returned 404 in this environment, so neither is used as behavioral evidence.

The successfully fetched Samsung Gallery page establishes the first-party Gallery reference. The sample’s Pictures-first discovery, in-place search, detail inspection, unavailable/empty recovery, and explicit destructive confirmation are restrained category adaptations; they do not claim undocumented Samsung implementation details and do not reproduce Samsung branding, imagery, account content, or device-specific navigation.

| Reference-derived convention | PhotoGallery adaptation for Tizen NUI | Deliberate deviation / reason |
|---|---|---|
| Pictures is the discovery surface; individual photos lead to a detail context. | `Pictures` opens on a date-labelled photo grid; Enter/tap opens `Photo details`. | Tizen TV uses four large columns rather than a phone-density grid so remote focus is visible and hit targets stay large. |
| Search narrows the gallery rather than replacing it with unrelated navigation. | A visible `Search` command opens an in-place query state and an empty-results recovery command. Back or Cancel restores Pictures. | Search is text-only because location, paths, notes, faces, cloud, and account data are outside the privacy-bounded product scope. |
| Gallery is resilient when library content is unavailable or empty. | Loading, unavailable-media, empty-library, and no-result states have a visible recovery action. | The sample uses local geometric placeholders. Production must query `MediaContent`; fixtures are not product media. |
| A destructive removal needs an explicit decision. | Detail’s Delete opens a modal that traps focus. Back/Cancel returns focus to Delete; success restores a valid Pictures card. | “Moved out of this gallery view” is intentionally not a claim about Samsung Trash retention; actual MediaContent mutation semantics are an unresolved target capability gate. |
| One UI uses calm content hierarchy, readable type, and restrained emphasis. | White top bar, neutral surface, single blue focus/action color, compact rounded controls, no gradients/glass/dock/dashboard. | Focus uses blue outline **and** thumb border/scale, which is necessary for remote accessibility and is not a visual copy. |

## Executable sample

- Canonical preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)
- Local fixtures: six non-identifying geometric placeholder photos. They contain no paths, locations, people, accounts, or remote images.
- State reducer: `window.photoGalleryPreview.command(type, value)` is the single command path used by pointer/touch and keyboard. Supported commands are `openSearch`, `closeSearch`, `setQuery`, `openDetail`, `back`, `askDelete`, `cancelDelete`, `confirmDelete`, `retry`, and the test-only `simulate`.
- Input: pointer/touch click; Arrow keys emulate D-pad traversal; Enter/Space activate; Escape emulates Back. Search Escape restores Pictures. The Delete modal traps the rendered focus list and restores focus deterministically.
- Scaling: a single centered 1920×1080 canvas transform maps the browser canvas to its viewport. Production NUI must use the corresponding inset-aware ancestor transform, not manual independent scaling.

### Sample state inventory and implementation mapping

| Sample control/state | NUI/TizenFX implementation target | Domain command/state | Action / ViewAnnotation / A2UI mapping |
|---|---|---|---|
| Pictures header and `Search` button | `TextLabel` + focusable `Button` in `PhotoGallery.App` header | `GalleryScreen.Pictures`; `OpenSearch` | No public navigation Action. Header is not annotated. A2UI surface reports current `pictures` state and available `search` control. |
| Date group and photo card | Real NUI image actor/thumbnail loader with a focusable per-card hit surface | `PhotoRecord` snapshot, `SelectPhoto(id)`, `OpenDetail(id)` | Visible card publishes View ID `pictures:<id>`, `Tizen.Entity.Photo` identity, canonical generated `Photo.ToJson()` in `Annotation.EntityInfo`, real bounds/focus. `View_ToPresentation` derives current photo A2UI from that snapshot. |
| Search field, Cancel, result grid | NUI text input plus focusable Cancel and photo-card actors | `GalleryScreen.Search`, bounded `PhotoSearchCriteria`, cancellable `SearchAsync` | `Tv_Tizen.Action.Photo_Search` shares `PhotoQueryService`; typed invalid/unavailable failure maps to error. A2UI reports query/result/empty status without raw paths or notes. |
| Loading grid state | NUI progress/placeholder actors | `GalleryLoadState.Loading`; refresh generation | No stale annotations. A2UI represents loading only; no canned photo content. |
| Empty library / no results | NUI label + focusable Refresh / Show all button | `GalleryLoadState.Empty` / `SearchEmpty`; `Refresh` / `ClearSearch` | No false Entity annotation. A2UI exposes recovery control and current empty reason. |
| Media unavailable + Try again | NUI error surface + Button | `GalleryLoadState.Unavailable`; capability/preflight result; `Retry` | Provider returns typed capability-unavailable rather than advertising a fake media operation. A2UI includes bounded error code/message. |
| Detail header Back, image, metadata | NUI `View` image surface, `TextLabel`, Buttons | `GalleryScreen.Detail`, selected ID; `BackToPictures` | Detail publishes View ID `detail:<id>` while visible. `Tv_Tizen.Action.Photo_ToPresentation` and `View_ToPresentation` must build separate current `surfaceUpdate` / `dataModelUpdate` JSON from the same generated Entity snapshot. |
| Delete and confirmation modal | NUI modal overlay with explicit focus trap and restoring `FocusManager` target | `RequestDelete(id)`, `CancelDelete`, `ConfirmDelete` | `Tv_Tizen.Action.Photo_DeleteImage` only after target MediaContent delete capability is proven. On success, refresh/resolver verifies postcondition; modal is removed from annotations. A2UI reports confirmation and resulting state, never a static fixture. |
| Focus ring / selection / resize | NUI `FocusManager`, measured `CalculateScreenPositionSize()`, physical root plus transformed canvas | Focused view ID, selected Entity ID, viewport state | `GetAnnotatedViews`, `GetFocusedView`, `FindById` reflect only live views; enclosing `Tizen.Entity.View` owns measured bounds and `IsFocused`. |

## A2UI current-state contract

PhotoGallery supports Presentation through `Tv_Tizen.Action.Photo_ToPresentation` and the required View `ToPresentation` path. Therefore every production presentation must be generated from the **current generated Photo Entity snapshot and rendered reducer state**, not from the browser fixture.

| Producer | Required output | Consumer and proof still required |
|---|---|---|
| `Tv_Tizen.Action.Photo_ToPresentation` for a resolved current `Photo` | Valid JSON `Presentation.Template` with `surfaceUpdate`, and independent JSON `Presentation.Document` with matching `dataModelUpdate`; bounded One UI profile semantics for title/image state, selection, availability, and controls | DisplayPresentation’s versioned Samsung One UI A2UI profile must render the real PhotoGallery output on the Common Emulator. No target evidence yet. |
| `Common_Tizen.Action.View_ToPresentation` for `pictures:<id>` / `detail:<id>` | Same two JSON documents derived from `Annotation.EntityInfo` created by generated `Photo.ToJson()`, plus live focus/visible state | Discover annotated View, parse nested `EntityInfo`, call View action, then compare DisplayPresentation render. No provider or target evidence yet. |

Privacy boundary: external presentation may use a title and stable ID only after the generated schema is inspected. It must not expose raw MediaContent paths, location, notes, thumbnail bytes, accounts, or unbounded metadata. The exact generated `Photo.ToJson()` fields and projection policy remain a code-generation gate.

## Capture and comparison ledger

| Slice/state | HTML capture | Installed Aurum capture | Comparison: hierarchy / geometry / type / spacing / color / controls / density / focus / state / scaling | Status |
|---|---|---|---|---|
| Pictures, initial focused search | Playwright Chromium headless [1920×1080 capture](images/html-pictures-1920x1080.png) | Not implemented/installed | Browser hierarchy and Search focus render; native comparison remains unavailable. | Browser pass / native open |
| Pictures, focused photo card | D-pad ArrowRight asserted the first card focus cue | Not implemented/installed | Browser focus movement works; native card and measured focus remain unavailable. | Browser pass / native open |
| Search, matching results | `Morning` query produced one result, then pointer activation opened detail | Not implemented/installed | Browser query/result flow works; native text input and cancellable real media query remain unavailable. | Browser pass / native open |
| Search, empty results | `missing` query rendered no-result recovery; Show all returned Pictures | Not implemented/installed | Browser recovery works; native route remains unavailable. | Browser pass / native open |
| Detail | Search result pointer activation rendered detail | Not implemented/installed | Browser detail works; target thumbnail loading remains unavailable. | Browser pass / native open |
| Delete confirmation / Cancel focus restoration | Escape closed modal and restored `Delete`; confirmation returned to Pictures with visible outcome | Not implemented/installed | Browser modal focus defect was fixed; actual mutation capability and NUI modal remain unavailable. | Browser pass / native open |
| Media unavailable | Test-only state injection rendered unavailable; Retry returned Pictures and restored Search focus | Not implemented/installed | The preview state is covered, but target capability preflight remains unavailable. | Browser pass / native open |
| Loading / responsive canvas | Test-only state injection [1920×1080](images/html-loading-1920x1080.png) and [1280×720](images/html-loading-1280x720.png) captures; smaller viewport asserted a non-identity canvas transform | Not implemented/installed | Browser loading and reference-canvas scaling work; native viewport behavior remains unavailable. | Browser pass / native open |

## Evidence boundary

Browser verification may prove the sample’s interaction and responsive canvas only. On 2026-08-11, Playwright Chromium headless ran D-pad card focus, Pictures → Search matching/no-result → Detail → Delete cancel/confirm plus injected unavailable/loading states, and retained the linked 1920×1080 and 1280×720 PNG captures. The injected states are test seams, not user-reachable product commands. It does **not** prove MediaContent access, generated bindings, NUI rendering, package signing, provider discovery, ViewAnnotation, A2UI, DisplayPresentation rendering, Aurum input, or Telegram screenshot delivery. Each must be added to this ledger with retained installed-target evidence before the corresponding status becomes pass.
