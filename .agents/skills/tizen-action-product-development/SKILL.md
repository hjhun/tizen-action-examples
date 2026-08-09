---
name: tizen-action-product-development
description: Use when designing, implementing, or validating product-grade Tizen Action example apps.
version: 1.0.0
author: Hermes Agent
license: MIT
metadata:
  hermes:
    tags: [tizen, action-framework, nui, one-ui, entity, viewannotation, aurum, e2e]
    related_skills: [tizen-aurum-ui-automation]
---

# Product-Grade Tizen Action Example Development

## Overview

Use this skill for every new or materially expanded app in this repository. An example app is a real, independently useful product flow that exposes Action, Entity, and ViewAnnotation capabilities to an Agent; it is not a screen mockup or a provider that returns canned data.

Read the repository `AGENTS.md`, `docs/DASHBOARD.md`, `docs/TIZEN_ACTION_DOMAIN_DEVELOPMENT_GUIDE.md`, and `.agents/workflows/NUI_SCALING_AND_UI_EVIDENCE.md` before changing an app. Use the adjacent `tizen-aurum-ui-automation` skill for native UI verification and screenshots.

## Required Application Layout

For every new application, create a top-level `<Name>/` directory before implementation. `<Name>` is the product/domain name used by this repository (for example `Browser`), while its platform Action category is documented separately as `Tizen.Action.<Name>` in the app docs and Dashboard. Preserve the Calendar/Reminder project topology: application-facing NUI, domain, persistence, use-case, typed Action provider, typed View provider, and host-runnable tests are separate siblings. Do not collapse them into a single `src/` project.

```text
<Name>/
├── README.md
├── README_Eng.md
├── refs/
│   └── one-ui-design.html
├── docs/
│   ├── README.md
│   ├── README_Eng.md
│   ├── DEVELOPMENT_GUIDE.md
│   ├── DEVELOPMENT_GUIDE_Eng.md
│   └── images/
├── src/
│   ├── <Name>.App/
│   ├── <Name>.Domain/
│   ├── <Name>.Persistence/
│   ├── <Name>.UseCases/
│   ├── <Name>.ActionProvider/
│   └── <Name>.ViewActionProvider/
├── tests/
│   ├── <Name>.Domain.Tests/
│   ├── <Name>.UseCases.Tests/
│   ├── <Name>.Persistence.Tests/
│   ├── <Name>.App.Tests/
│   └── <Name>.ActionProvider.Tests/
└── dist/                        # generated package artifacts; ignored
```

Add a domain-specific provider project only where the platform category requires one (for example Calendar's `Calendar.ScheduleActionProvider`); keep its generated binding inside that provider project. Keep `refs/`, `docs/`, `src/`, and `tests/` at the same levels used by Calendar and Reminder. Existing differently named application directories are legacy/project state; do not rename or migrate them unless the Goal explicitly includes that work.

The app manifest must use the Dashboard's canonical identity:

```text
package ID = org.tizen.<name>
application ID = org.tizen.<name>
```

Do not put `actionexample` or `actionexamples` in newly created IDs. Do not reuse platform provider `details.appid` values.

## Architecture and Product Gate

Complete an Architect stage before implementation. It must record functional requirements, non-functional requirements, product flows, quality attributes, risks, domain model, UI/provider/use-case/persistence boundaries, and measurable acceptance criteria.

Treat Galaxy One UI as the interaction reference:

- Use calm, content-first hierarchy, restrained accent colors, clear typography, predictable navigation, meaningful empty/loading/error states, and confirmation for destructive actions.
- Adapt Galaxy patterns to Tizen NUI, remote/D-pad, keyboard, pointer, and touch rather than reproducing proprietary screens or assets.
- Define initial focus, directional focus order, Enter activation, Back behavior, modal focus trapping, and focus restoration for every primary page and overlay.
- Make localization, scalable text, contrast, bounded data, and accessible labels part of the initial product design.

Before source code, create `refs/one-ui-design.html`. It is the detailed, app-specific design reference, not a decorative mockup. It must cover:

1. Product goal, target users, non-goals, and One UI reference rationale.
2. Screen inventory and information architecture.
3. Every primary page, empty/loading/error state, editor/detail/confirmation overlay, and navigation transition.
4. Remote/D-pad, keyboard, pointer, and touch interaction and focus rules.
5. Typography, spacing, color, elevation, component states, accessibility, and responsive/reference-canvas scaling rules.
6. Action-to-product mapping, Entity identity/context, and ViewAnnotation surfaces.
7. Testable acceptance criteria and screenshot coverage plan.

When meaningful design alternatives exist, document at least two options and the selected trade-off before coding.

## Product-Realism Gate

Implement the actual product capability named by the Goal. A simulated core capability is allowed only when a real platform/hardware capability is unavailable on the chosen target, and the simulation must be explicitly bounded and documented.

Examples:

- A Browser app must integrate an actual web engine or web runtime and load reachable web content. A fake address bar with static pages does not satisfy a Browser Goal.
- A media app must provide a real, app-owned library/playback integration where the target permits it; deterministic fixtures may support tests but cannot replace the principal product behavior.
- A hardware/system feature may expose an unavailable result only after capability detection. Do not claim a Common Emulator simulator proves a TV/device capability.

Record the concrete runtime dependency, capability preflight, failure behavior, and target limitations in the app guide and E2E evidence.

## Action, Entity, and Agent Utility

Design Action contracts for Agent discovery and reliable composition, not merely for UI convenience.

1. Prefer an existing domain category when it already owns the Entity. Generate the complete category with `actionc -a <category>` so positional method IDs remain stable.
2. Use generated Entity DTOs at the Action boundary and app-owned models in domain/UI code. Use generated `ToJson()` for serialized Entity context.
3. For mutable Entity products, provide stable-ID retrieval and search/discovery paths, preserve requested resolver order and duplicates, validate bounded inputs, and verify mutation postconditions through public Actions.
4. Keep provider methods thin: validation, DTO/domain conversion, use-case call, and typed result/status mapping. UI and providers use the same application-owned command/query service instance; no self-RPC.
5. For each advertised Action, define the Agent intent it serves, discovery inputs, success output, bounded failure status, required follow-up Action, and user-visible effect. Add this matrix to the app's development guide.

### Custom extensions

If platform Actions or Entities genuinely cannot express a required product capability, define an app-owned custom category instead of changing platform-owned schemas. Use this naming shape:

```text
Tizen.Action.<Name>Custom_<Verb>
```

For example: `Tizen.Action.MusicCustom_QueueNext`.

Before adding one, document the platform-contract gap, why an existing Action cannot be composed to solve it, its input/output Entity contract, provider metadata, Agent discovery description, and E2E coverage. Treat the custom category as an app contract: generate it, version it, and never hand-edit generated bindings.

## ViewAnnotation and Presentation

ViewAnnotation is Agent-facing contextual state. Publish only meaningful, currently rendered, non-sensitive views.

- Each annotated view has a stable per-surface View ID and the annotated domain Entity's stable `EntityType` and `EntityId`.
- Set `Annotation.EntityInfo` from the generated Entity `ToJson()` snapshot; do not hand-build a parallel JSON projection.
- Derive `ScreenBounds`, optional `WindowBounds`, visibility, and focus from the real NUI view where the platform provides a stable seam. Bounds belong to the enclosing View, not the nested Annotation.
- Synchronize publication with rendering, data changes, focus transitions, overlay lifecycle, pause/resume, and removal. Do not expose stale or invisible views.
- Implement applicable `GetAnnotatedViews`, `GetFocusedView`, `FindById`, and `View_ToPresentation` paths consistently. `View_ToPresentation` must return separate valid JSON `surfaceUpdate` template and `dataModelUpdate` document that correspond to the same Entity context.
- Include an Agent-task matrix in the guide: natural-language user goal → discovery Action → Entity/Annotation context → control Action → observable UI/postcondition.

## NUI Scaling and Input

Use an inset-aware, centered uniform reference-canvas transform for new NUI apps. Derive drawable area from `Window.Default.WindowSize` and `GetInsets()`. Scale page content, typography, spacing, radii, borders, overlays, and focus geometry exactly once. Keep the physical root background full-window.

Do not replace a valid root during transient invalid resize/inset geometry. Publish only finite, positive, measured transformed bounds. Host geometry tests are useful but do not substitute for rendered target validation.

## TizenFX, .NET Standard, Responsiveness, and Async

Use Tizen Device APIs through TizenFX whenever they provide the target capability, lifecycle integration, storage, connectivity, application control, media, or platform service required by the product. Use .NET Standard APIs for portable domain logic, collections, serialization, I/O abstractions, networking primitives, cancellation, and concurrency where they are the clearer fit. Keep Tizen-specific dependencies behind adapters so domain and use-case tests remain host-runnable.

- Keep the NUI/UI thread free of network, web-engine startup, storage scans, parsing, and long-running Action work.
- Use `async`/`await` end to end for naturally asynchronous operations; do not block with `.Result`, `.Wait()`, or polling sleeps.
- Accept and propagate `CancellationToken` for user-cancellable navigation, search, loading, download, and shutdown work. Cancel superseded requests when a new intent replaces them.
- Bound concurrency, queues, collection sizes, retries, and timeouts. Use immutable snapshots or synchronization for shared state observed by UI and Action providers.
- Marshal only the final state update to the NUI thread and verify lifecycle safety when callbacks complete after pause, navigation, disposal, or app termination.
- Surface responsive loading, progress, timeout, offline, retry, cancellation, and partial-failure states instead of freezing or silently failing.
- Prefer event-driven TizenFX APIs over host-style polling, and dispose subscriptions, handles, streams, clients, and platform resources deterministically.
- Add focused tests for cancellation, stale-result suppression, concurrent provider/UI access, bounded failure, and resource cleanup where applicable.

## Build, Package, and Signing Evidence

Report host tests, build, package inspection, install/runtime, and native UI evidence independently.

- Run focused domain/use-case tests before broader checks.
- Build with the project's real Tizen tooling and inspect the produced package payload and managed/native dependencies.
- Choose signing mode explicitly for the target. Do not call any signing choice a “default profile,” do not enter or reveal certificate secrets, and do not infer certificate health from configuration display alone.
- Treat a Public Common Emulator package as Common Emulator evidence only; report TV/product-target validation separately.

## Aurum UI Automation and Screenshot Evidence

Use `.agents/skills/tizen-aurum-ui-automation/SKILL.md` after a real package is installed on the selected target.

1. Record target serial, app state, app ID, bootstrap availability, Aurum transport health, target resolution, and tree capability.
2. Prefer remote keys for D-pad semantics; use target-native coordinates only after examining the current frame and resolution.
3. After every state-changing input, capture and inspect a fresh native screenshot. Transport success alone is not proof of a UI transition.
4. Cover home/default, every primary page/tab, search/editor, applied results, detail, focus state, error/empty state, and safe destructive confirmation where applicable.
5. Store validated screenshots in `docs/images/` with descriptive stable names. Verify format, dimensions, decoding, and relative links.
6. When an autonomous Goal has a configured messaging destination, send every newly created validated app screenshot there as native media with a concise app/screen/state caption. Do not send temporary, invalid, duplicate, secret-bearing, authentication, permission, payment, or unrelated frames.
7. Record target profile, resolution, fixture provenance, automation command, and known accessibility-tree limitations in the app README. Remove only the SDB forward/session state owned by the run.

## Documentation Deliverables

Write bilingual, separated application documentation after behavior is implemented and evidence is captured.

- `docs/README.md` is Korean and links to `README_Eng.md`; `docs/README_Eng.md` is English and links back.
- `docs/DEVELOPMENT_GUIDE.md` is Korean and links to `DEVELOPMENT_GUIDE_Eng.md`; `docs/DEVELOPMENT_GUIDE_Eng.md` is English and links back.
- Match section structure, examples, terminology, tables, diagrams, screenshot coverage, navigation, and local links across languages. Keep Action names, Entity names, manifest keys, JSON fields, and commands untranslated.
- Follow the paired Korean/English index and numbered-guide style under `<tizen-action-repo>/docs/guide-2.0/` as the structural reference.
- The app-level `README.md` at the app root summarizes product behavior and links to the bilingual docs. Only add screenshot galleries after the referenced target captures exist.
- The development guide must include architecture, setup/build/package commands, Action/Entity/custom-category contracts, ViewAnnotation/A2UI contract, Agent-task matrix, scaling/input policy, Aurum capture procedure, verification matrix, limitations, and screenshot provenance.

Validate every Markdown link, fenced JSON/YAML example, image decode/dimensions, and Korean/English parity before declaring documentation complete.

## Completion Checklist

- [ ] `<Name>/` exists before implementation, and its app docs map it to the applicable `Tizen.Action.<Name>` category; detailed `refs/one-ui-design.html` exists.
- [ ] Architect analysis establishes product-level functional/non-functional requirements and acceptance criteria.
- [ ] Product core is real on the chosen target, or an explicit simulator/capability limitation is documented.
- [ ] Actions, Entities, resolver/search behavior, typed failures, and postconditions are tested.
- [ ] Custom Actions, if any, use an app-owned `Tizen.Action.<Name>Custom_<Verb>` contract with documentation and E2E.
- [ ] Current NUI views publish useful, correct ViewAnnotation context and applicable A2UI presentation.
- [ ] NUI scaling and all supported input/focus flows are verified on a rendered target.
- [ ] Aurum evidence covers each page and interaction state; screenshots are valid and repository-stable.
- [ ] Korean and English docs are separate, mutually linked, equivalent, and include the final screenshot gallery.
- [ ] README claims map to recorded build/package/target/UI evidence without overstating Common Emulator validation.
