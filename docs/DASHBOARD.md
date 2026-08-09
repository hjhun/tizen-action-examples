# Tizen Action Framework 2.0 Example Apps Dashboard

Last reviewed: 2026-08-09 09:15 KST

## Purpose

This is the durable development queue for independently useful Tizen Action Framework 2.0 example apps. Each checkbox represents one product-grade domain app, not an isolated Action demo. An item may be checked only after its declared Action, Entity, ViewAnnotation, NUI, packaging, and applicable target verification gates have actual evidence.

The source catalog is [TIZEN_ACTION_2_0_DOMAIN_APP_CATALOG.md](TIZEN_ACTION_2_0_DOMAIN_APP_CATALOG.md). Application-specific requirements, design decisions, execution state, and test evidence belong in that application's directory or its local `.dev/autonomous-goals/<goal-id>/` state.

## Identity Convention

All new example application manifests must use the following canonical identity convention:

- Package ID: `org.tizen.<name>`
- Application ID: `org.tizen.<name>`
- `<name>` is the lower-case product/domain name shown in this dashboard; do not use `actionexample` or `actionexamples` in new IDs.
- The example provider must not reuse a platform-owned `details.appid` from the Action catalog.

Existing Calendar and Reminder manifests still use historical `org.tizen.actionexamples.*` identities. This dashboard records their intended canonical IDs but does **not** change any manifest as part of dashboard preparation. Identity migration is a separately reviewed compatibility/packaging task.

## Completion Contract

Before checking an app complete, its autonomous Goal must establish and verify all applicable items below.

- Product: a source-backed Android Samsung stock-app/One UI design adapted to Tizen NUI, with deterministic D-pad/remote, keyboard, pointer, and touch behavior as applicable; loading, empty, validation, error, destructive-confirmation, focus-restoration, and Back behavior are part of the product scope. Use Samsung Internet for Browser, Samsung Gallery for PhotoGallery, and relevant Samsung app/system components for generic A2UI surfaces. Preserve the Samsung mental model and component behavior; arbitrary visual invention must not be labeled One UI.
- UI contract: one browser-verified executable `<App>/refs/one-ui-sample.html` previews the actual app flow and states; requirements stay in Markdown, obsolete HTML is removed, and `<App>/docs/UI_PARITY.md` continuously compares sample captures with installed Aurum screenshots.
- Architecture: architect analysis records functional and non-functional requirements, authoritative UI references and adaptation decisions, domain/use-case/provider/UI boundaries, persistence and external-effect ownership, security/privacy constraints, failure behavior, and observable acceptance criteria before implementation starts.
- Action and Entity: complete category generation preserves `action.seq` method IDs; advertised Actions have positive and bounded-negative calls; mutations prove their postconditions through Search/GetByIds or equivalent stable-ID resolvers.
- ViewAnnotation: meaningful current views publish actual Entity identity plus generated `ToJson()` snapshot, use current `Annotation.EntityInfo`, remain synchronized with visibility/focus/lifecycle, and support applicable discovery, lookup, and focused-view paths.
- Presentation/A2UI: every app that exposes or consumes `Presentation`, implements `ToPresentation`/`View_ToPresentation`, or integrates with DisplayPresentation must implement current-state A2UI and prove both Action → DisplayPresentation and ViewAnnotation → Presentation round trips on the Common Emulator; canned fixtures are not completion evidence. `DisplayPresentation` itself must be a Samsung One UI A2UI renderer with a versioned semantic-component → reusable One UI NUI mapping, not a generic title/body card or arbitrary payload skin.
- Verification: host/domain tests, build, package payload inspection, and target checks are reported independently. Common Emulator evidence is never reported as TV/product-specific validation.
- Delivery: no claim of completion is made without retained commands, outputs, and known limitations in the Goal evidence.

## Completed / Existing Reference Apps

- [x] Calendar — P0 reference implementation
  - Canonical package/app ID for future migration: `org.tizen.calendar`
  - Current historical manifest identity: `org.tizen.actionexamples.calendar`
  - Scope evidenced in repository: Calendar CRUD, stable Entity identity/resolver, search, persistence/reminder reconciliation, NUI views, ViewAnnotation, and A2UI presentation.
  - Any new autonomous work must preserve the user-deleted `docs/specs/2026-08-08-calendar-navigation-search-view-design.md` unless the Goal explicitly adopts that change.

- [x] Reminder — P0 reference implementation
  - Canonical package/app ID for future migration: `org.tizen.reminder`
  - Current historical manifest identity: `org.tizen.actionexamples.reminder`
  - Scope evidenced in repository: Schedule Action provider, reminder/reservation flows, persistence, NUI focused workspace, ViewAnnotation, and Common Emulator UI evidence.

## Ready Queue — P0 State, Entity, and Presentation Baseline

- [ ] Browser — `Tizen.Action.Browser`
  - Package/app ID: `org.tizen.browser`
  - Product baseline: browser workspace with URL navigation, current-page context, history/bookmark-style discovery, detail, and calendar handoff.
  - Action/Entity baseline: URL Go, current-page lookup, `GetBrowserByIds`, page-to-calendar conversion, presentation.
  - Note: `Browser/` currently exists as an untracked user workspace. An autonomous Goal must not modify it until explicitly adopted as agent-owned scope.

- [ ] PhotoGallery — `Tizen.Action.Photo`
  - Package/app ID: `org.tizen.photogallery`
  - Product baseline: gallery browse/search, photo detail, delete confirmation, and presentation.
  - Action/Entity baseline: add/delete/search, stable-ID lookup, gallery presentation.
  - Note: `PhotoGallery/` currently exists as an untracked user workspace; preserve it until explicitly adopted.

- [ ] DisplayPresentation — `Tizen.Action.Display` infrastructure fixture
  - Package/app ID: `org.tizen.displaypresentation`
  - Product baseline: deterministic renderer for a provider-produced `Presentation`.
  - Action/Entity baseline: typed presentation display, linked from apps that publish `View_ToPresentation`.

## Planned Queue — P1 Search, Catalog, and Integration

- [ ] MusicLibrary — `Tizen.Action.Music`
  - Package/app ID: `org.tizen.musiclibrary`
  - Scope: search/play/playlist mutation and Album, Artist, Playlist, Station resolvers.
  - Note: `Music/` currently exists as an untracked user workspace; preserve it until explicitly adopted.

- [ ] VideoCatalog — `Tizen.Action.Video`
  - Package/app ID: `org.tizen.videocatalog`
  - Scope: filtered content search, GetContentByIds, details, play/control, directory play, presentation.
  - Note: `Video/` currently exists as an untracked user workspace; preserve it until explicitly adopted.

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
