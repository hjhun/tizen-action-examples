# Calendar 2.0 Navigation, Views, Search, and Action Implementation Plan

> **For Hermes:** Execute task-by-task with strict RED–GREEN–REFACTOR, independent review, host build gates, and Common Emulator E2E.

**Goal:** Complete the approved Calendar 2.0 command bar, Month/Week/Day/Agenda views, advanced period search, additive typed search Action, focus/accessibility behavior, and rendered-card ViewAnnotation lifecycle.

**Architecture:** Pure Tizen-free state/presentation/search models feed small NUI renderers. UI and providers share app-owned domain query services; the app never invokes its own Action RPC. Existing Calendar Search stays ABI-compatible while a new typed period Action is appended and regenerated through the full category.

**Tech stack:** C#/.NET 8, Tizen NUI, TIDL/actionc, JSON Action/Entity schemas, custom executable test projects, Tizen CLI packaging, SDB/action-tool.

---

## Preconditions and baselines

1. Record Git status and connected SDB target.
2. Run host test baseline: Domain, Persistence, UseCases, App.
3. Build Calendar.ActionProvider, Calendar.ViewActionProvider, and Calendar.App.
4. Package/install current app and prove existing Calendar/View Action discovery.
5. Preserve generated source and existing action.seq order.

## Task 1: Period navigation state and command bar semantics

**Files:**
- Modify `Calendar/src/Calendar.App/CalendarUiState.cs`
- Modify `Calendar/src/Calendar.App/CalendarUiCommand.cs`
- Modify `Calendar/tests/Calendar.App.Tests/Program.cs`

**TDD loop:**
1. Add failing tests for previous/next Month, leap year, Jan/Dec, day clamp, Today, and focus restoration.
2. Run App tests and verify expected failures.
3. Add `CalendarViewMode`, explicit header focus regions, previous/next commands, and reducer transitions.
4. Run targeted and complete App tests.

## Task 2: View-mode presentation models

**Files:**
- Create `Calendar/src/Calendar.App/CalendarPeriodPresentation.cs`
- Create/modify view-specific pure presentation files
- Modify `Calendar/tests/Calendar.App.Tests/Program.cs`

**TDD loop:**
1. Add one failing tracer test for each Month/Week/Day/Agenda period and event grouping behavior.
2. Implement minimal pure models.
3. Add boundary/empty/ordering tests.
4. Refactor shared period calculations only after green.

## Task 3: Advanced domain search

**Files:**
- Create `Calendar/src/Calendar.Domain/CalendarSearchCriteria.cs`
- Modify `Calendar/src/Calendar.Domain/CalendarEventRepository.cs`
- Modify `Calendar/tests/Calendar.Domain.Tests/Program.cs`
- Modify provider/use-case tests as needed

**TDD loop:**
1. Add failures for keyword + title/location/note fields, start-inclusive/end-exclusive overlap, limits, ordering, and invalid range.
2. Implement one immutable criteria/query seam used by UI and provider.
3. Keep existing `Search(string?)` compatibility by delegating to the new query path.
4. Run Domain and broader host tests.

## Task 4: NUI command bar and four renderers

**Files:**
- Modify `Calendar/src/Calendar.App/CalendarMonthView.cs`
- Create `Calendar/src/Calendar.App/CalendarCommandBarView.cs`
- Create Week/Day/Agenda view builders
- Modify `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify `Calendar/src/Calendar.App/CalendarTheme.cs`
- Modify App tests

**TDD loop:**
1. Add source-contract and pure state failures for controls, commands, mode switching, and D-pad routes.
2. Implement independent previous/Today/next/search/mode hit surfaces through `CalendarTouchBinder`.
3. Implement selected-mode rendering from pure presentation models.
4. Synchronize logical focus and stable semantic focus IDs.
5. Build after each vertical view slice.

## Task 5: Advanced search overlay and recovery states

**Files:**
- Create `Calendar/src/Calendar.App/CalendarSearchView.cs`
- Modify `Calendar/src/Calendar.App/CalendarOverlayView.cs`
- Modify `Calendar/src/Calendar.App/CalendarApplication.cs`
- Modify App tests

**TDD loop:**
1. Add failures for immutable draft/applied criteria, invalid dates, focus order, result selection, empty state, and Back restoration.
2. Implement the overlay using app-owned domain query service.
3. Add visible save/delete/restore/search errors and focusable recovery controls.
4. Verify no UI-to-own-Action RPC dependency.

## Task 6: Additive typed period-search Action

**Files:**
- Create `appfw/tizen-action/default-actions/entities/Tizen.Entity.CalendarSearchQuery.entity`
- Create `appfw/tizen-action/default-actions/actions/Tv_Tizen.Action.Calendar_SearchInPeriod.action`
- Append `appfw/tizen-action/default-actions/action.seq`
- Regenerate `Calendar/src/Calendar.ActionProvider/Generated/CalendarActionProvider.cs`
- Modify `Calendar/src/Calendar.ActionProvider/CalendarService.cs`
- Modify manifest/provider tests

**TDD loop:**
1. Add failing schema/order/provider contract tests before schema or production changes.
2. Append the method without moving existing Calendar method positions.
3. Run `actionc -a Tizen.Action.Calendar` with extensionless output; never edit generated code.
4. Implement the generated method by adapting typed criteria to the shared domain search.
5. Compile-probe and byte-compare regeneration.

## Task 7: Accurate ViewAnnotation lifecycle

**Files:**
- Modify `Calendar/src/Calendar.ViewActionProvider/CalendarViewService.cs`
- Modify host/application publication seams
- Modify View/App tests

**TDD loop:**
1. Add failures proving only rendered event cards publish, stale cards clear, focused event tracks actual semantic focus, and all modes/search work.
2. Publish mode-specific rendered cards and clear on inactive/overlay/lifecycle transitions.
3. Preserve generated Entity `ToJson()`, type, ID, and A2UI ToPresentation.
4. Capture actual bounds where NUI exposes stable geometry; otherwise retain and clearly report synthetic bounds.

## Task 8: Accessibility and focus acceptance

**Files:** NUI view builders, theme, overlay, tests.

1. Add tests/contract assertions for accessible names, states, complete weekday names, and deterministic focus transitions.
2. Apply NUI accessibility metadata where supported by the target SDK.
3. Restore real focus by stable semantic ID after rerender, modal close, mutation, and errors.
4. Verify focus has outline plus background/scale and no dead end.

## Task 9: Full verification and independent review

1. Run all host-compatible test projects.
2. Build all app/provider projects with zero new warnings/errors.
3. Run generation reproducibility and schema JSON/XML validation.
4. Run static security scan and independent reviewer agents; fix/reverify blocking findings.
5. Run `git diff --check` and inspect all generated/manual boundaries.
6. Update `.dev/DASHBOARD.md`, `.dev/progress/developer.md`, and `.dev/DEVELOPTMENT.md`.

## Task 10: Common Emulator E2E loop

1. Confirm SDB target/capabilities and profile.
2. Package a signed emulator TPK from the complete managed output staging root.
3. Inspect archive signatures, manifest, and required DLLs.
4. Install and launch app.
5. Verify Calendar/View/new period-search Action discovery and typed execution with explicit appid.
6. Exercise pointer/remote navigation, all four modes, advanced search, CRUD, restart persistence, focused/annotated views, and A2UI presentation.
7. Capture screenshots only after functional paths pass; use agent-browser for browser-hosted design/evidence pages when useful.
8. Repeat fix → host tests → package → device E2E until all acceptance criteria pass or a genuine external blocker is proven.

## Risks

- Existing repository content is largely untracked; stage/commit only explicitly requested artifacts and do not overwrite unrelated work.
- Adding a platform default Action requires cross-repository generated binding refresh and runtime schema availability on the emulator.
- NUI focus/accessibility API availability may differ on Common Emulator; compile against actual SDK and verify device behavior.
- The full scope is large; keep each vertical slice green and avoid one monolithic renderer/application class.
