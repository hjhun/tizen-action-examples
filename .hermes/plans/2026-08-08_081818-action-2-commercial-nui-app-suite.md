# Tizen Action 2.0 Commercial NUI Application Suite Implementation Plan

> **For Hermes:** Implement in small verified increments. Treat every category application as a production-quality TV product sample, not as a UI mock or a schema-only provider.

**Goal:** Build a repository of independently installable Tizen .NET/NUIApplication examples—one polished app per externally usable Default Action category—so Action Framework 2.0 can be demonstrated through real, discoverable, executable applications. The initial execution target is the public Tizen 10.1 common emulator; the UI remains TV remote-first and a later TV-target pass will validate profile-specific packaging and behavior.

**Architecture:** The repository will contain a shared, versioned C# platform library plus one packageable NUI app per category (`Calendar/`, `Music/`, and so on). Each app owns durable domain data, an accessible TV-first UI, remote focus navigation, light/dark themes, Action 2.0 provider integration generated from the platform schemas, and an Agent-facing View Annotation adapter. The platform Action schemas remain platform-owned and are never copied or changed in this repository.

**Tech Stack:** C#; .NET 8 / `net8.0-tizen10.0`; TizenFX; Tizen NUI; Tizen CLI; SDB; public TV emulator; `actionc -l C#`; generated TIDL/rpc-port service stubs; RPM-backed package manifests; Tizen Action Framework 2.0.

---

## 1. Source-grounded findings

### Default Action catalog

The installed Default Action catalog contains **117 actions in 22 categories**. Twenty-one are product-domain categories suitable for a provider app; `Tizen.Internal.Action.View` is platform integration used by all UI apps rather than a standalone consumer application.

| Category | Actions | Example app directory | Product role |
|---|---:|---|---|
| `Tizen.Action.Accessibility` | 2 | `Accessibility/` | accessibility preferences and status |
| `Tizen.Action.App` | 4 | `App/` | installed/running-app launcher and search |
| `Tizen.Action.Art` | 4 | `Art/` | ambient art and gallery viewer |
| `Tizen.Action.Broadcast` | 13 | `Broadcast/` | channel, program guide, recording experience |
| `Tizen.Action.Browser` | 4 | `Browser/` | TV browser and handoff/conversion flows |
| `Tizen.Action.Calendar` | 5 | `Calendar/` | calendar agenda, event management, presentation |
| `Tizen.Action.Camera` | 5 | `Camera/` | camera input/capture control |
| `Tizen.Action.Display` | 1 | `Display/` | display presentation/gallery |
| `Tizen.Action.Game` | 3 | `Game/` | game discovery and launch hub |
| `Tizen.Action.Health` | 4 | `Health/` | wellness dashboard and workout flows |
| `Tizen.Action.Home` | 2 | `Home/` | TV home-page navigation |
| `Tizen.Action.IoT` | 4 | `IoT/` | home-device and scene controller |
| `Tizen.Action.MultiView` | 13 | `MultiView/` | multi-app/PiP workspace |
| `Tizen.Action.Music` | 13 | `Music/` | music library and playback experience |
| `Tizen.Action.Photo` | 4 | `Photo/` | photo library and slideshow |
| `Tizen.Action.Schedule` | 10 | `Schedule/` | TV recordings, viewing and reminders |
| `Tizen.Action.ScreenShare` | 2 | `ScreenShare/` | screen-share session controller |
| `Tizen.Action.Settings` | 4 | `Settings/` | searchable TV settings experience |
| `Tizen.Action.Support` | 4 | `Support/` | device support, update, diagnosis and manuals |
| `Tizen.Action.Video` | 6 | `Video/` | video library/player |
| `Tizen.Action.Volume` | 6 | `Volume/` | sound control panel |
| `Tizen.Internal.Action.View` | 4 | shared only | annotated-view discovery / presentation contract |

The Calendar contract is immediately usable as the first vertical slice: it has five `tidl` Actions (`AddEvent`, `RemoveEvent`, `Search`, `ToPresentation`, `UpdateEvent`) and its platform Entity contains `Title`, `StartDate`, `EndDate`, `Note`, and `Location`.

### Framework implementation rules that shape every app

1. Platform-owned Actions are provided by generating the installed category with `actionc -a <category> -l C#`; generated sources are committed and never hand-edited.
2. Each provider manifest must declare one `http://tizen.org/metadata/action/provider` entry for each exact Action name it implements—category metadata is insufficient.
3. A TIDL Action `details.appid` must match the provider app ID. The app/package resource root must contain its manifest and Action Framework resources in the locations expected by the package manager.
4. The provider implementation must be restart-safe. `allowedBackground` and `autoDispose` are connection behavior, not a durable-state guarantee.
5. `action.seq` is a platform ABI contract. This examples repository consumes Default Actions and must not edit/reorder the platform action catalog.
6. `Tizen.Entity.View.Annotation.EntityJson` is the required canonical `ToJson()` snapshot of the annotated Entity at annotation-publication time. `EntityType` and `EntityId` are optional stable identity hints for a later refresh. `EntityJson` gives an Agent a single-hop, self-contained semantic payload; it is contract-defined, size-bounded, and excludes secrets or an unneeded full persisted-model copy.

### Important compatibility gates

- `actionc --help` confirms C# is a supported generation target. The actual generated C# service and its rpc-port dependencies must nevertheless be compiled and executed in a NUI application before the suite architecture is replicated.
- The local native SDK currently lists only legacy public C# templates while the requested target is TV/.NET 8. A modern NUIApplication bootstrap must be created from the installed `TizenNUITemplate`/SDK-compatible project assets rather than selecting a legacy generic template by name.
- A public emulator now permits root shell and has a rebuilt Action DB. Every sample package must be explicitly registered with `unified-backend --preload -y <package-id>` after RPM installation; RPM placement alone does not register Action metadata.
- The current `action-tool list-actions` command produced no textual listing even while direct SQLite verification showed 117 registered actions. Therefore package E2E must use `get-action`, `get-entity`, `search --json`, actual `execute` callbacks, and DB/package evidence—not a silent list command alone.

---

## 2. Repository architecture

```text
tizen-action-examples/
├── README.md
├── AGENTS.md
├── Directory.Build.props
├── Directory.Packages.props
├── docs/
│   ├── architecture.md
│   ├── build-and-deploy.md
│   ├── action-provider-contract.md
│   ├── design-system.md
│   ├── view-annotation.md
│   ├── quality-gates.md
│   └── categories/<Category>.md
├── shared/
│   ├── ActionExamples.Foundation/       # result/status, clocks, persistence, logging
│   ├── ActionExamples.Nui/              # shell, navigation, focus, theme, reusable controls
│   ├── ActionExamples.Action/           # generated-code boundary, provider registration helpers
│   ├── ActionExamples.ViewAnnotation/   # mapped semantic views and annotations
│   ├── ActionExamples.Testing/          # fixtures, fake clock/storage, screenshot/test helpers
│   └── ActionExamples.Build/            # package/manifest generation helpers
├── Calendar/
│   ├── src/Calendar.App/                # NUIApplication presentation and input
│   ├── src/Calendar.Domain/             # event aggregate and use cases
│   ├── src/Calendar.Infrastructure/     # durable repository, clock, import adapter
│   ├── src/Calendar.ActionProvider/     # generated C# stub + ServiceBase implementation
│   ├── resources/                       # app visual assets and package resources
│   ├── tests/                           # unit, UI-model, contract and device runner tests
│   ├── packaging/                       # manifest, TPK/RPM packaging inputs
│   └── README.md
├── Music/
├── ... one directory for each product category ...
└── tools/
    ├── generate-provider.sh
    ├── build-category.sh
    ├── deploy-category.sh
    ├── verify-category.sh
    └── verify-all.sh
```

### App contract

Every category app must provide all of the following before it can be called complete:

- `NUIApplication` startup, responsive 16:9 TV layout, remote D-pad/Enter/Back support, focus restoration, no pointer-only operation.
- Theme tokens for light and dark mode, contrast/focus-visible rules, dynamic text scaling where supported, localized strings, empty/loading/error/offline states.
- A real domain model with persistent, versioned local storage and deterministic sample/demo data only when clearly labeled.
- A category-specific information architecture, not one generic CRUD screen copied across domains.
- Category Actions implemented against actual domain use cases; `Tizen.Entity.Status` failures must be actionable and structured.
- Stable object IDs and View annotations for meaningful focusable cards/rows/edit controls; annotations refer to the actual category Entity ID and type.
- A package manifest with the exact provider metadata, least-privilege declaration, a valid installed executable, and a reproducible package layout.
- Unit/contract tests, target Action discovery tests, target Action execution success/failure tests, remote-input tests, and visual/manual acceptance evidence.

---

## 3. Delivery order

The order optimizes for shared design-system maturity, Action composition, and demonstrable TV value. A category is not started until the preceding category meets all quality gates.

### Wave 0 — Compatibility and product foundation

1. Prove a minimal .NET 8 TV `NUIApplication` can build, sign, package, install, launch, receive remote keys, and emit runtime logs on the public emulator.
2. Prove a C# `actionc`-generated ServiceBase can coexist with an NUIApplication, listen through rpc-port, register provider metadata, and return a real `Tizen.Entity.Status` over `action-tool execute`.
3. Prove the View Annotation path on an actual NUI control. If a public TizenFX binding is absent, document and isolate the smallest supported native bridge; do not invent an unverified annotation protocol.
4. Implement the shared shell, design tokens, focus manager, theme manager, persistence abstraction, Action provider host, View Annotation adapter, and test/deploy tooling.

### Wave 1 — Calendar reference application

5. Build `Calendar/` as the reference-quality app and documentation anchor.
6. Deliver month/week/day/agenda modes; create/edit/delete flow; recurring-event policy; conflict indication; search; local persistence; event detail; empty/offline/error states; light/dark theme; remote-focused dialogs.
7. Implement all five platform Calendar Actions and validate them from an Agent/tool path. `ToPresentation` returns a compact view/presentation model; all event-related visible controls carry stable Calendar annotations.
8. Produce an end-to-end guide that shows schema discovery, action selection, request JSON, result JSON, UI effect, provider restart behavior, and uninstall cleanup.

### Wave 2 — Personal media and information applications

9. `Photo/`: library, date/location search, album and slideshow experience; `Photo` annotations.
10. `Music/`: library/search, album/artist/playlist views, queue and transport state; `Music`/`Playlist` annotations.
11. `Video/`: browse/details/playback controls/directory flow; `Content`/`Movie` annotations.
12. `Art/`: ambient artwork browser and presentation mode; `Artwork` annotations.
13. `Browser/`: TV-safe browsing/history/bookmarks and calendar/presentation conversion actions; `Browser` annotations.

### Wave 3 — TV-native utility and system-control applications

14. `Settings/`, `Accessibility/`, and `Volume/` as a coherent but separately packageable settings family.
15. `App/`, `Home/`, and `Game/` as a launcher/discovery family.
16. `Display/`, `ScreenShare/`, and `MultiView/` as a display/workspace family.
17. `Support/` for diagnostics, updates, device information, and manuals.

### Wave 4 — Live services and scheduling applications

18. `Broadcast/` and `Schedule/` together, with recording/viewing/reminder flows and explicit unavailable-tuner behavior.
19. `IoT/` with device inventory, state, controls, scenes, confirmation and failure states.
20. `Camera/` with camera-source selection, capture state, privacy indicator, and unavailable-device behavior.
21. `Health/` with wellness summaries/workout flows and conservative local-data/privacy behavior.

For categories depending on a real platform capability unavailable in the emulator (tuner, camera, IoT controller, account-linked media), deliver a polished deterministic simulator/adapter with an unmistakable “Demo provider” state, then add a separately documented production adapter interface. Never claim a synthetic action result represents real device control.

---

## 4. Execution tasks

### Task 1: Establish the compatibility baseline

**Objective:** Create verifiable evidence that the selected NUI/.NET/TV/Action SDK combination can support the planned architecture.

**Files:**
- Create: `docs/environment-baseline.md`
- Create: `tools/check-environment.sh`
- Create: `Bootstrap/` temporary compatibility project; remove or convert it into `shared/` only after it passes

**Steps:**
1. Record `dotnet`, `tizen`, `sdb`, `actionc`, `tidlc`, installed TizenFX/NUI/runtime RPM versions, public-emulator capability, TV profile, and signing profile availability.
2. Generate a current TV-compatible NUIApplication project using the installed SDK assets; target `net8.0-tizen10.0` only after inspecting the SDK API metadata.
3. Build, package, install, launch, capture runtime log, and verify D-pad/Back input.
4. Generate one C# platform-category stub with `actionc -a Tizen.Action.Calendar -l C#`; compile it with its rpc-port dependency in an NUIApplication host.
5. Add temporary exact Calendar provider metadata, preload the package, call one Action, and verify a non-synthetic callback result.

**Validation:** Phase 0 pipeline passes end-to-end on `emulator-26101`; no C#-generated-code or runtime-linker uncertainty remains.

### Task 2: Define the shared commercial TV design system

**Objective:** Establish reusable quality constraints before any category UI is built.

**Files:**
- Create: `shared/ActionExamples.Nui/`
- Create: `docs/design-system.md`
- Create: `docs/accessibility-and-remote.md`
- Test: `shared/ActionExamples.Nui.Tests/`

**Steps:**
1. Define spacing, typography, color, elevation, focus-ring, motion-reduction, and light/dark token sets.
2. Implement `TvApplicationShell`, route stack, remote-key dispatcher, roving focus, modal focus trap, list/grid virtualization policy, toast/error presentation, and theme persistence.
3. Add keyboard/remote unit tests for focus order and Back behavior.
4. Capture approved emulator reference screenshots for both themes.

**Validation:** A shell showcase runs on emulator and every control can be operated with D-pad/Enter/Back only.

### Task 3: Implement the Action provider and View Annotation foundations

**Objective:** Make Action 2.0 and Agent-visible UI behavior reusable and testable.

**Files:**
- Create: `shared/ActionExamples.Action/`
- Create: `shared/ActionExamples.ViewAnnotation/`
- Create: `tools/generate-provider.sh`
- Create: `docs/action-provider-contract.md`
- Create: `docs/view-annotation.md`

**Steps:**
1. Implement generation wrappers that keep schemas platform-owned and generated C# files isolated from handwritten domain code.
2. Define a provider-host lifecycle that maps generated ServiceBase calls to async domain use cases without blocking NUI UI state.
3. Implement package-manifest templates that produce one exact provider metadata element per supplied Action.
4. Map NUI semantic controls to `Tizen.Entity.View` fields. Enforce annotation `EntityId` equals the referenced domain Entity’s stable ID.
5. Add a target-side introspection harness for annotated views. The harness must use the real supported framework bridge/API discovered in Task 1.

**Validation:** A test app returns a status result from a generated C# provider and exposes focused/annotated NUI controls without duplicating sensitive entity payloads.

### Task 4: Implement Calendar as the reference vertical slice

**Objective:** Deliver the first commercial-quality, fully Action-enabled TV app.

**Files:**
- Create: `Calendar/src/Calendar.App/`
- Create: `Calendar/src/Calendar.Domain/`
- Create: `Calendar/src/Calendar.Infrastructure/`
- Create: `Calendar/src/Calendar.ActionProvider/`
- Create: `Calendar/tests/`
- Create: `Calendar/packaging/tizen-manifest.xml`
- Create: `Calendar/README.md`
- Create: `docs/categories/Calendar.md`

**Steps:**
1. Write domain tests for event validation, overlap/conflict policy, search, update/delete, time-zone policy, stable IDs, and persistence migrations.
2. Implement the local event repository and deterministic clock abstraction.
3. Implement month/week/day/agenda views, event editor, conflict feedback, search, context-aware focus, and themes.
4. Generate the Calendar C# provider, implement all five methods against the same domain use cases, and register every exact Calendar Action in the manifest.
5. Attach Calendar annotations to focusable events, editor fields, result rows, and presentation targets.
6. Package/sign/deploy/preload; verify schema discovery and all five Action success/failure paths against the actual NUI state.
7. Document execution JSON and visible expected state for every Action.

**Validation:** Host tests, clean package build, signed install, manifest preload, Action discovery, five executed Actions, remote UI flows, restart persistence, and uninstall cleanup all pass.

### Task 5: Add one category per accepted vertical-slice template

**Objective:** Scale without reducing product quality or duplicating architecture.

**Files:**
- Create: `<Category>/...` using the Calendar module shape
- Create: `docs/categories/<Category>.md`
- Modify: root `README.md`, category inventory, CI matrix

**For each category, repeat:**
1. Create a one-page category product brief: target TV scenario, navigation map, supported Action-to-use-case table, Entity/annotation map, storage model, capability dependencies, unavailable-capability UX, and privacy/security requirements.
2. Write domain and Action contract tests before provider implementation.
3. Build the NUI interaction model and visual acceptance screens before wiring device-specific adapters.
4. Generate C# provider stubs from the supplied category and implement exactly the Actions published in the manifest.
5. Add View Annotation coverage and agent execution documentation.
6. Complete the same host/package/emulator quality gate as Calendar.
7. Only then enable the next category.

### Task 6: Documentation and release-grade verification

**Objective:** Make the suite a trustworthy external Action Framework 2.0 guide.

**Files:**
- Create: `docs/quickstart.md`
- Create: `docs/category-matrix.md`
- Create: `docs/agent-walkthroughs.md`
- Create: `docs/architecture.md`
- Create: `tools/verify-all.sh`

**Steps:**
1. Document the distinction between platform Action provider and app-defined Action developer roles.
2. Publish each app’s Action schemas, provider metadata, generated-code procedure, entity semantics, annotations, remote UX, packaging and target verification.
3. Provide copy-pasteable Agent discovery/execution examples that identify the requested provider app explicitly.
4. Generate a release matrix showing per-category build, package, preload, discovery, execution, remote UI, theme, annotation, and real-adapter/demonstration status.
5. Add screenshots/video capture only after their behavior is verified on the public emulator.

---

## 5. Non-negotiable quality gates

A category cannot be marked complete until all are true:

- [ ] Builds cleanly with the selected .NET/Tizen target and has no uncommitted generated-code drift.
- [ ] Produces a signed package and installs/launches on the public TV emulator.
- [ ] Is explicitly backend-registered after RPM deployment and its package manifest/resources are present at the expected installed paths.
- [ ] `get-action`, `get-entity` where applicable, `search --json`, and real Action execution show the intended provider and contract.
- [ ] Every published Action has at least one valid, validation-error, unavailable-adapter, and restart/auto-dispose test where relevant.
- [ ] All core flows work by TV remote; focus never becomes lost, hidden, or pointer-dependent.
- [ ] Both themes have screenshot-reviewed focus, contrast, empty, error, loading, and modal states.
- [ ] Every important focusable UI object has an appropriate stable View Annotation and a verified `ToJson()` representation for transport; no secrets or duplicate persisted domain model are placed in a View Entity.
- [ ] Persistence is migration-tested, state survives provider recreation where needed, and demo data is clearly segregated from production adapters.
- [ ] Documentation provides actual device evidence rather than unverified intended behavior.

---

## 6. Risks and decisions requiring validation

1. **C# TIDL provider integration:** `actionc` advertises C# but the installed runtime/reference assemblies and NUIApplication lifecycle interoperability need a Phase 0 proof. Do not implement multiple apps before this passes.
2. **View Annotation bridge:** The schema is clear, but the API that publishes annotations from managed NUI controls has not yet been located in source. Validate this against the actual Framework API; if native-only, isolate a narrow supported interop library and test it on device.
3. **Platform category ownership:** Default Action `details.appid` currently names the framework service. Each example needs provider selection verified with its own appid and exact provider metadata, without altering the platform definitions.
4. **Simulator versus real capability:** Broadcast, camera, screen sharing, IoT, home/system and account-backed media cannot be represented as genuinely available merely because an Action succeeds. The UI and Action status must disclose adapter capability.
5. **Package format:** The requirements call for TPK packaging while the Action Framework provider guide uses RPM-backed manifest registration. Decide during Phase 0 whether each external example is directly packaged as a signed TPK, RPM-backed TPK payload, or both; the decision must preserve Action registration and public-emulator installation.
6. **Scope:** Twenty-one polished apps is a product portfolio, not one feature. Calendar is the first acceptance milestone; funding/schedule should be reviewed after Calendar and the shared foundation demonstrate the actual implementation cost.

---

## 7. First implementation milestone

**Milestone A acceptance:** Calendar is installed as a .NET/NUI TV app on the public emulator; is fully navigable by remote in both themes; provides all five `Tizen.Action.Calendar` Actions through a C# generated Action 2.0 provider; publishes verified View Annotations; persists state; and has reproducible build/package/preload/discovery/execution documentation.
