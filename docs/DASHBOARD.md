# Tizen Action Framework 2.0 Example Apps Dashboard

Current-state review: 2026-09-08. Previous portfolio review: 2026-08-09 09:15 KST.

## Purpose

This is the durable development queue for independently useful Tizen Action Framework 2.0 example apps. Each checkbox represents one product-grade domain app, not an isolated Action demo. An item may be checked only after its declared Action, Entity, ViewAnnotation, NUI, packaging, and applicable target verification gates have actual evidence.

The current [local contract snapshot](TIZEN_ACTION_2_0_DOMAIN_APP_CATALOG.md#현재-로컬-계약-snapshot-2026-09-08) separates generated default-category slots from manifest advertisements. It records local schema revision `38219b1e1ba347cd92178e25f8c90eefc8040d33`, not equality with an installed catalog. Older catalog tables remain historical. Application-specific requirements, design decisions, execution state, and test evidence belong in that application's directory or its local `.dev/autonomous-goals/<goal-id>/` state.

## Identity Convention

All new example application manifests must use the following canonical identity convention:

- Package ID: `org.tizen.<name>`
- Application ID: `org.tizen.<name>`
- `<name>` is the lower-case product/domain name shown in this dashboard; do not use `actionexample` or `actionexamples` in new IDs.
- The example provider must not reuse a platform-owned `details.appid` from the Action catalog.

Calendar and Reminder use canonical `org.tizen.calendar` and `org.tizen.reminder` package/application IDs. See the [identity migration record](../Calendar/docs/APP_ID_MIGRATION.md) for installation, data preservation and target verification.

## Completion Contract

Before checking an app complete, its autonomous Goal must establish and verify all applicable items below.

- Product: a source-backed Samsung stock-app/One UI design adapted to Tizen NUI, with deterministic D-pad/remote, keyboard, pointer, and touch behavior as applicable; loading, empty, validation, error, destructive-confirmation, focus-restoration, and Back behavior are part of the product scope. Use Samsung Internet for Browser, Samsung Gallery for PhotoGallery, Samsung Music for Music, Samsung Video for Video, and the nearest relevant Samsung app/system surface for other domains. Preserve the Samsung mental model and component behavior; arbitrary visual invention must not be labeled One UI.
- UI contract: one browser-verified executable `<App>/refs/one-ui-sample.html` previews the actual app flow and states; requirements stay in Markdown, obsolete HTML is removed, and `<App>/docs/UI_PARITY.md` continuously compares sample captures with installed Aurum screenshots.
- Architecture: architect analysis records functional and non-functional requirements, authoritative UI references and adaptation decisions, domain/use-case/provider/UI boundaries, persistence and external-effect ownership, security/privacy constraints, failure behavior, and observable acceptance criteria before implementation starts.
- Action and Entity: complete category generation preserves `action.seq` method IDs; advertised Actions have positive and bounded-negative calls; mutations prove their postconditions through Search/GetByIds or equivalent stable-ID resolvers.
- ViewAnnotation: meaningful current views publish actual Entity identity plus generated `ToJson()` snapshot, use current `Annotation.EntityInfo`, remain synchronized with visibility/focus/lifecycle, and support applicable discovery, lookup, and focused-view paths.
- Presentation/A2UI: every app that exposes or consumes `Presentation`, implements `ToPresentation`/`View_ToPresentation`, or integrates with DisplayPresentation must implement current-state A2UI and prove both Action → DisplayPresentation and ViewAnnotation → Presentation round trips on the Common Emulator; canned fixtures are not completion evidence. Preserve existing split `surfaceUpdate`/`dataModelUpdate` producers as a named legacy v0.8 compatibility profile, while canonical support follows a declared official Google A2UI version and negotiated catalog (upstream revision `ec97cb0d7499932e67003ffe5b709a3db7e7033a`: v0.9.1 Current Production, v1.0 Candidate). `DisplayPresentation` preserves the Google A2UI protocol/semantic contract and renders supported components through reusable Samsung One UI-adapted Tizen NUI components; it must not invent a dialect, generic title/body fallback, or arbitrary payload skin.
- Display hosting: a 32-bit ARGB8888 buffer, per-pixel-alpha native window, and A2UI logical surface are separate gates. A DisplayPresentation transparent overlay is capability-gated, never mandatory for semantic rendering, and always retains an opaque fallback. Completion requires installed Common Emulator screenshot/input/focus evidence for alpha compositing, declared hit regions/pass-through behavior, D-pad/key and pointer/touch focus restoration, lifecycle, and fallback; host compile, transparent View color, or transparency API invocation is not evidence.
- Verification: host/domain tests, build, package payload inspection, and target checks are reported independently. Common Emulator evidence is never reported as TV/product-specific validation.
- Delivery: no claim of completion is made without retained commands, outputs, and known limitations in the Goal evidence.

## Existing Reference Apps — Partial Acceptance

Previously accepted reference implementations and dated evidence remain accepted
within their original scope. Unchecked boxes retain the full product completion
gates above; they do not revoke those accepted steps. [Packages](../Packages/README.md)
records build/package results, not completion of native or product gates.

- [ ] Calendar — P0 reference implementation; accepted steps, open product gates
  - Package/app ID: `org.tizen.calendar`
  - Scope evidenced in repository: Calendar CRUD, stable Entity identity/resolver, search, persistence/reminder reconciliation, NUI views, ViewAnnotation, and A2UI presentation.
  - Current boundary: [118 host geometry checks and prior accepted results](../Calendar/docs/STAGE1_VALIDATION.md); current-ID UHD/DCI/8K native and SystemInfo initial sizing remain incomplete. The separate VM Action discovery `-111` cause remains unknown.
  - Any new autonomous work must preserve the user-deleted `docs/specs/2026-08-08-calendar-navigation-search-view-design.md` unless the Goal explicitly adopts that change.

- [ ] Reminder — P0 reference implementation; accepted steps, open product gates
  - Package/app ID: `org.tizen.reminder`
  - Current contract: standard Reminder 5 + View 4 + ReminderCustom 6 = 15 manifest advertisements; see [Reminder](../Reminder/README.md). Reservation custom Actions describe an app-owned simulator boundary, not absence of reservation capability in Broadcast.
  - Accepted reminder/reservation, persistence and UI evidence is retained. [FHD iconify focus regression](../Reminder/docs/UI_PARITY.md) preserves draft/validation/IME in that case; general Home, exact skipped-event/no-followup-resize, actor/caret identity, modal/stale fallback, serviceChanged focus and high-resolution/initial-sizing gates remain open.

## Ready Queue — P0 State, Entity, and Presentation Baseline

- [ ] Browser — `Tizen.Action.Browser`
  - Package/app ID: `org.tizen.browser`
  - Product baseline: browser workspace with URL navigation, current-page context, history/bookmark-style discovery, detail, and calendar handoff.
  - Current [P1 contract](../Browser/docs/ACTION_CONTRACT_VALIDATION.md): full Browser 18/View 4 generated slots; Browser category advertises only GetCurrentPage/GetTabs/ToPresentation (3), plus View 4/custom resolver 1 = 8 app advertisements. The other 15 Browser Actions are unadvertised bounded failures, not completed features.
  - Committed UI navigation/tabs remain. Standard OpenPage/P2–P5 are blocked on public navigation-event attribution/terminal guarantees; host injected late-event RED is not proof of a native framework bug. Old GetCurrent/Go/GetBrowserByIds/ToCalendar advertisements were removed.

- [ ] PhotoGallery — `Tizen.Action.Photo`
  - Package/app ID: `org.tizen.photogallery`
  - Product baseline: gallery browse/search, photo detail, delete confirmation, and presentation.
  - Action/Entity baseline: add/delete/search, stable-ID lookup, gallery presentation.
  - Committed implementation; [stable delete target FHD fixture and host coverage](../PhotoGallery/docs/UI_PARITY.md) are accepted in their recorded scopes. Native SystemInfo exception/partial reads, initial sizing and native8K remain open.

- [ ] DisplayPresentation — `Tizen.Action.Presentation` infrastructure fixture (API 14; legacy integration follow-up in [validation record](../DisplayPresentation/docs/2026-09-06-interop-followup.md))
  - Package/app ID: `org.tizen.displaypresentation`
  - Product baseline: deterministic Google A2UI-compatible renderer for a provider-produced `Presentation`; supported semantics use Samsung One UI-adapted Tizen NUI presentation and run in either a capability-verified transparent overlay or the mandatory opaque full-window fallback.
  - Action/Entity baseline: typed presentation display, linked from apps that publish `View_ToPresentation`.
  - Implemented: legacy v0.8 split `surfaceUpdate`/`dataModelUpdate` compatibility with paging; Presentation 1 + View 4 = 5 manifest advertisements. [54 Browser/PhotoGallery host producer checks](../DisplayPresentation/docs/UI_PARITY.md#browser--photogallery-producer-regression-2026-09-08) supplement prior Calendar/shared checks; they are not native RPC/paging/focus/annotation acceptance.
  - Canonical version/catalog/lifecycle/action support and transparent hosting remain unimplemented/unverified as applicable. Historical Pending tables keep their original date/payload scope; later limited native cases do not mark all rows PASS.
  - Overlay evidence gate: Common Emulator native screenshots plus input/focus traces; no support claim from host build, ARGB8888 alone, `SetTransparency`, or transparent root/View color.

## Planned Queue — P1 Search, Catalog, and Integration

The unstarted P1–P3 domain names/contracts below are historical planning proposals.
Reconcile each against the then-current schema when starting it; this review does
not redesign or approve implementation of those domains.

- [ ] MusicLibrary — `Tizen.Action.Music`
  - Package/app ID: `org.tizen.musiclibrary`
  - Scope: search/play/playlist mutation and Album, Artist, Playlist, Station resolvers.
  - `Music/` contains a tracked design-reference HTML only; no executable app is implemented. Preserve the reference.

- [ ] VideoCatalog — `Tizen.Action.Video`
  - Package/app ID: `org.tizen.videocatalog`
  - Scope: filtered content search, GetContentByIds, details, play/control, directory play, presentation.
  - `Video/` contains a tracked design-reference HTML only; no executable app is implemented. Preserve the reference.

- [ ] BroadcastGuide — `Tizen.Action.Broadcast`
  - Package/app ID: `org.tizen.broadcastguide`
  - Scope: channel/EPG search, tune/record/playback, Channel/Program/RecordedProgram resolvers, guide launch.

- [ ] IoTHome — `Tizen.Action.IoT`
  - Package/app ID: `org.tizen.iothome`
  - Scope: device list/status, bounded control, scene execution, authorization and command validation.

- [ ] SettingsCenter — `Tizen.Action.Settings`
  - Package/app ID: `org.tizen.settingscenter`
  - Scope: setting search/get/set/open, persistence, type/range validation.

- [ ] AppHub — `Tizen.Action.App`
  - Package/app ID: `org.tizen.apphub`
  - Scope: installed/running app discovery, search, deep-link launch, store-detail navigation.

## Planned Queue — P2 Product Features and Composite State

- [ ] GameHub — `Tizen.Action.Game`
  - Package/app ID: `org.tizen.gamehub`
  - Scope: game search/launch and game-bar opening with an app-launch adapter.

- [ ] HealthCoach — `Tizen.Action.Health`
  - Package/app ID: `org.tizen.healthcoach`
  - Scope: workout search/start, daily summary, presentation, minimal sensitive-data projection.

- [ ] ArtGallery — `Tizen.Action.Art`
  - Package/app ID: `org.tizen.artgallery`
  - Scope: current artwork, search/show, presentation, display integration.

- [ ] CameraCapture — `Tizen.Action.Camera`
  - Package/app ID: `org.tizen.cameracapture`
  - Scope: device select/switch, capture mode, start/stop, explicit hardware-unavailable behavior.

- [ ] ScreenShare — `Tizen.Action.ScreenShare`
  - Package/app ID: `org.tizen.screenshare`
  - Scope: cast/mirroring lifecycle, source validation, cleanup.

- [ ] MultiViewManager — `Tizen.Action.MultiView`
  - Package/app ID: `org.tizen.multiviewmanager`
  - Scope: split/PIP lifecycle, placement/removal, focus/fullscreen/size/sound-focus transitions.

## Planned Queue — P3 Global System Controls

- [ ] HomeNavigator — `Tizen.Action.Home`
  - Package/app ID: `org.tizen.homenavigator`
  - Scope: current page lookup and page switch; global navigation remains behind a simulator/adapter boundary.

- [ ] AccessibilityControl — `Tizen.Action.Accessibility`
  - Package/app ID: `org.tizen.accessibilitycontrol`
  - Scope: feature state query/set, permission, reversible state, assistive UX.

- [ ] DeviceSupport — `Tizen.Action.Support`
  - Package/app ID: `org.tizen.devicesupport`
  - Scope: device information, update check, manual page, diagnosis; immutable information and triggers stay separate.

- [ ] VolumeControl — `Tizen.Action.Volume`
  - Package/app ID: `org.tizen.volumecontrol`
  - Scope: get/set/raise/lower/mute/unmute, range checks, mute transition, idempotence.

## Autonomous Development Selection Rules

1. Work from P0 through P3 unless a Goal explicitly selects another checked-off item.
2. Prefer an unowned, clean project directory. Do not adopt a pre-existing untracked directory automatically.
3. Before any app implementation, the Architect stage must produce a detailed product-level requirements and design artifact. “Action works” is insufficient product scope.
4. One autonomous portfolio batch may own up to three explicitly named apps concurrently. Each app keeps independent Goal state and disjoint app-directory ownership; shared Dashboard, solution, manifest, Git index, commit, and push operations are coordinated separately.
5. Do not call the signing choice a “default profile.” Packaging/signing mode is selected explicitly per target and is recorded as build evidence; no signing profile is implied by this dashboard.
6. For GitHub repositories, a Goal may commit and push only when its Goal explicitly authorizes it and the repository's remote/branch policy is verified. For Gerrit repositories, a Goal updates the existing Change-Id as an amended patchset rather than creating a parallel commit series.
7. Each autonomous app worker runs on a recurring one-minute cadence and reports every run, meaningful milestone, blocker, screenshot evidence, and final verification to the configured Telegram destination.
