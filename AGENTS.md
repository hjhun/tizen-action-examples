# Tizen Action Framework 2.0 Example Apps

## Project Mission

This repository develops Tizen Action Framework 2.0 and demonstrates it through complete, domain-focused example applications. Each example should show how an application exposes useful capabilities to agents by combining Actions, Entities, and ViewAnnotations with a polished, usable Tizen UI.

Use Samsung's first-party Galaxy applications as the primary product reference for interaction patterns, information hierarchy, visual refinement, and feature completeness. Adapt those ideas to Tizen capabilities and target input methods instead of copying proprietary assets or reproducing a Galaxy screen mechanically.

## Core Development Principles

### Think Before Coding

- State assumptions when requirements, platform behavior, schemas, or target capabilities are unclear.
- Inspect the repository, generated bindings, platform schemas, and existing examples before choosing an implementation.
- Surface meaningful alternatives and tradeoffs instead of silently selecting one interpretation.
- Prefer the simplest approach that satisfies the observable requirement.
- Ask for clarification when an unresolved ambiguity would materially change behavior, compatibility, or scope.

### Keep the Solution Simple

- Implement only the requested behavior; do not add speculative features or configurability.
- Avoid abstractions that have only one use unless they create a necessary platform or test seam.
- Keep generated provider adapters thin and move business behavior into testable domain or use-case code.
- If an implementation becomes disproportionately large, reconsider the design before extending it.

### Make Surgical Changes

- Touch only files and lines required by the task.
- Preserve existing architecture, naming, formatting, and user-authored changes.
- Do not refactor adjacent code or remove pre-existing dead code unless explicitly requested.
- Remove only the unused code or artifacts introduced by the current change.
- Never edit generated Action binding source manually. Change its schema or generator input and regenerate it.

### Work Toward Verifiable Outcomes

- Translate each request into observable acceptance criteria before implementation.
- For behavior changes, prefer a focused failing test, the minimum passing implementation, and then a small cleanup.
- Run the narrowest relevant verification first, followed by broader regression checks when feasible.
- Report each verification layer separately. A host test, build, package, emulator Action call, and visual UI check prove different things.
- Never claim a target, input mode, or runtime was verified unless it was actually exercised.

## Action Framework Requirements

### Actions

- Treat platform Action schemas and generated bindings as contracts.
- Generate the complete Action category so positional method identifiers remain compatible; never reorder existing entries in `action.seq`.
- Validate inputs at the provider boundary and return explicit typed statuses for success, invalid input, missing data, unavailable capability, and internal failure as supported by the schema.
- Keep provider methods focused on validation, Entity conversion, use-case invocation, and result mapping.
- Verify the real wire format from generated dispatch code and target execution rather than guessing its JSON shape.

### Entities

- Use generated Entity types at the Action boundary and domain models inside the application when separation improves testability.
- Give persisted Entities stable identifiers that do not change after creation.
- Preserve request order in batch resolvers, including duplicate identifiers, and report unresolved identifiers explicitly.
- Keep Entity serialization consistent across providers, persistence, presentations, and annotations. Prefer generated serialization methods such as `ToJson()` when available.
- Bound collection sizes, identifier lengths, queries, and other externally supplied data.

### ViewAnnotations

- Annotate meaningful, currently rendered UI elements with the correct Entity type, stable Entity ID, and serialized Entity snapshot.
- Keep annotations synchronized with view creation, updates, focus changes, and removal. Do not expose stale or invisible views as current UI state.
- Implement focused-view lookup, annotated-view discovery, ID lookup, and View-to-Presentation conversion consistently when the applicable View Actions are exposed.
- Derive bounds and focus from actual UI state whenever the platform provides a reliable seam; document any synthetic fallback.
- Treat ViewAnnotation data as an agent-facing contract: deterministic, complete enough to act on, and free of unrelated sensitive UI state.

## Architecture

Keep dependencies directed inward:

```text
Tizen NUI / Action providers / platform adapters
                      |
                      v
                  Use cases
                      |
                      v
                    Domain
```

- Keep domain and use-case code runnable without Tizen runtime assemblies whenever practical.
- Put persistence, alarms, notifications, and other platform services behind narrow adapters.
- When UI and providers run in one process, inject the same repositories and command services into both so state remains consistent without self-RPC.
- For state changes with external side effects, persist the desired state before publishing it in memory and compensate only resources created by the failed operation.
- Track and modify only application-owned alarms, files, jobs, notifications, or other external handles.
- Keep generated code, domain logic, application UI, platform adapters, and tests in clearly separated projects or directories.

## Samsung-Inspired Product and UI Direction

- Study relevant Samsung Galaxy applications for layout hierarchy, navigation, editing flows, empty states, search, confirmation, feedback, and accessibility.
- Aim for calm, content-first screens with clear typography, intentional spacing, restrained color, and obvious primary actions.
- Build coherent end-to-end workflows, not isolated demo controls. The user should be able to create or discover an item, inspect it, modify it, and observe the resulting Action/Entity state.
- Preserve familiar platform behavior while adapting touch-oriented Galaxy patterns to every supported Tizen input method, including remote/D-pad, keyboard, pointer, and touch where applicable.
- Make focus visible and deterministic. Define sensible initial focus, directional navigation, back behavior, modal focus trapping, and focus restoration.
- Provide accessible labels, sufficient contrast, scalable text, bounded content, loading and empty states, validation feedback, and confirmation for destructive actions.
- For new or migrated reference-canvas NUI apps, derive the drawable area from `Window.Default.WindowSize` and `GetInsets()`, then prefer one centered uniform ancestor transform so page, overlay, typography, border, radius, and focus geometry scale exactly once. Existing manual-scaling apps must prove those same properties are each scaled once without duplicate root/local offsets.
- Validate resize/inset geometry before replacing the current root. Ancestor-transform apps publish only finite, positive measured View bounds; existing manual compatibility fallbacks require explicit scaled-size and non-1.0 native-bounds verification.
- Reuse project theme tokens and shared components where they already exist, but do not introduce a design-system abstraction solely for one screen.

## Testing and Completion Gates

Use the applicable gates independently:

1. Domain and use-case tests cover business rules, stable IDs, resolver ordering, validation, persistence transitions, compensation, restoration, and concurrency-sensitive repositories.
2. Project builds prove source and generated bindings compile with the expected references.
3. Packaging checks prove the manifest, payload, and signatures form a valid TPK.
4. Emulator or device Action tests prove provider discovery, actual wire format, typed RPC results, and postconditions.
5. UI acceptance proves visual layout, focus navigation, supported input methods, editing, error states, and ViewAnnotation lifecycle.
6. Product-specific validation is reported separately from Public Common Emulator validation.

For each advertised Action, test at least one successful invocation and one meaningful bounded failure case. After mutations, verify the resulting state through the relevant search or resolver Action, not only through the UI.

## Repository Guidance

- Treat `Calendar/` as the current reference implementation, not as a template that must be copied wholesale.
- Consult `docs/TIZEN_ACTION_DOMAIN_DEVELOPMENT_GUIDE.md` for detailed engineering and validation guidance.
- Follow `.agents/workflows/NUI_SCALING_AND_UI_EVIDENCE.md` when implementing reference-canvas scaling, capturing native UI states, or publishing README screenshot evidence.
- Apply `.agents/skills/tizen-action-product-development/SKILL.md` before designing, implementing, or materially expanding any example app; use its product, One UI design-reference, Agent contract, Aurum, screenshot, and bilingual documentation gates.
- Consult `docs/TIZEN_ACTION_2_0_DOMAIN_APP_CATALOG.md` for planned application domains and minimum scenarios.
- Keep application-specific documentation beside its application when practical.
- Preserve existing untracked files, local patches, generated artifacts, and development records unless the task explicitly covers them.
- Do not commit, push, install packages on external targets, or alter platform-owned schemas unless explicitly requested.

## Definition of Done

A change is complete when its requested behavior is implemented with the smallest coherent change, relevant automated checks pass, applicable Action/Entity/ViewAnnotation contracts are verified, and any unverified runtime or visual gate is stated plainly. Update documentation when user-facing behavior, integration contracts, build steps, or validation procedures change.
