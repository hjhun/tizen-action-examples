# Browser clean redevelopment contract baseline

검사 시각: 2026-08-11  
범위: `Browser/`만. Calendar/Reminder는 선행 migration이 완료된 읽기 전용 진단 대상이며 수정하거나 재생성하지 않는다.

## 계약 상태와 증거 규칙

이 문서는 `BROWSER-CONTRACT-002`의 현재 구현 계약이다. [`REDEVELOPMENT_INVENTORY.md`](REDEVELOPMENT_INVENTORY.md)에서 분류한 승인 입력을 하나의 구현 기준으로 통합한다.

- 이 문서의 **현재 계약**은 다음 clean slice의 테스트와 구현을 구속한다.
- 이전 Browser source, package, emulator RPC, native frame, `TRACEABILITY.md`, `STAGE3_VALIDATION.md`의 결과는 **historical-only**다. 새 source/build/package/target의 PASS로 재사용하지 않는다.
- 현재 authoritative Action catalog는 별도 framework checkout의 `default-actions/`다. Browser는 platform schema, `action.seq`, generator, generated output을 변경하지 않는다.
- Graphify graph는 사전 발견에 사용했다. 현재 graph에는 legacy `TizenInternalActionView` 노드가 남아 있으므로 View naming의 authority가 아니다.

## 제품과 경계

| 항목 | 현재 계약 |
|---|---|
| 제품 identity | package/application ID는 `org.tizen.browser`; Action category identity와 다르다. |
| 핵심 경험 | Samsung Internet의 compact address + Reload, page-first WebView, 독립 Tabs manager, Cancel/red Close confirmation 정신 모델을 1920×1080 Tizen NUI에 번역한다. Samsung branding/assets를 복제하지 않는다. |
| 실제 capability | `Tizen.NUI.BaseComponents.WebView`를 `IWebRuntime` 뒤에서 사용하여 실제 HTTP/HTTPS page를 load한다. static fixture page는 host/HTML test 보조물일 뿐 Browser capability를 대체하지 않는다. |
| 명시 범위 | normal-mode 1~20 tabs, current-page projection, navigation/history, bounded recovery, session restore. |
| 범위 밖 | Secret mode, account/sync, downloads, extensions, bookmarks/history UI, AI, remote thumbnails, permission auto-approval. |
| privacy | user-info/query/fragment, cookie, credential, form value, page body, private mode, raw engine error는 persistence, Entity, ViewAnnotation, A2UI, fixture, report에 넣지 않는다. |

### 구현 의존성 방향

```text
NUI App + WebView adapter + typed Browser/View providers
                         ↓
                      UseCases
                         ↓
                        Domain
                         ↓
          Persistence + portable runtime abstractions
```

Domain/UseCases/Persistence는 Tizen-free로 유지한다. NUI UI와 provider는 같은 command/query service 및 immutable current-state snapshot을 주입받으며, UI가 자기 provider RPC를 호출하지 않는다. mutation은 persist-before-publish이고, navigation은 cancellation/monotonic intent로 stale completion을 버린다.

## UI, 상태, 입력 계약

`refs/one-ui-sample.html`이 유일한 executable reference다. 다음 slice는 이 HTML을 browser-verify하고 state/keyboard-D-pad/pointer/touch behavior를 변경 없이 확정한 뒤에만 NUI UI를 수정한다.

| Surface/state | 필수 화면·행동 | focus/Back |
|---|---|---|
| Home/address edit | compact top address shell, Reload, local start content, tab count, dock | startup Home에서는 non-text command를 initial focus로 두어 OSK를 열지 않는다. address editing은 명시 activation으로만 시작한다. |
| Loading/Page | immediate progress, real WebView content, Back/Forward/Reload/Tabs | active navigation은 탭당 하나이며 newer intent가 prior intent를 취소한다. restored Page focus는 terminal intent가 확인된 뒤만 WebView로 이동한다. |
| error/offline/timeout/engine unavailable | bounded reason plus Retry/Back recovery | recovery control은 focus dead end가 아니어야 한다. |
| Tabs | ordered cards, selected cue, New tab, Close | first focus is selected card; max 20에서 New tab은 disabled and explained. |
| close confirmation | bounded title, Cancel/Close only | modal traps focus, Cancel is initial, Back=Cancel, completion restores invoking/nearest visible control. |

The physical root remains full-window. All product content is a single centered uniform 1920×1080 ancestor canvas transform calculated from `Window.Default.WindowSize` and `GetInsets()`. Typography, geometry, focus outline, radius, border, page, and modal scale once. Invalid/transient geometry retains the preceding valid root. Published View bounds are real finite positive post-transform NUI measurements, not design-canvas coordinates.

## FR/NFR acceptance baseline

The stable acceptance IDs are `FR-BROWSER-001` through `FR-BROWSER-026` and `NFR-BROWSER-001` through `NFR-BROWSER-027` from [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) and [`QUALITY_REQUIREMENTS.md`](QUALITY_REQUIREMENTS.md). The implementation slices must retain those IDs; this consolidated grouping defines their required evidence.

| Contract group | IDs | required observable result |
|---|---|---|
| Shell/navigation | FR-001~011; NFR-001~006, 010, 015~018 | responsive Home/loading/page/recovery and real WebView; 15-second timeout; cancellation and stale-result suppression; no UI-thread blocking; resize/lifecycle safety. |
| Tabs/session | FR-012~018; NFR-007~008, 013~014 | stable non-reused IDs, ordered max-20 workspace, close modal/restoration, versioned ≤256KiB atomic persistence. |
| Agent/View/A2UI | FR-019~026; NFR-009, 019~023 | bounded/redacted current state, complete generated categories, resolver semantics, current measured View snapshots, canonical/legacy A2UI separation. |
| accessibility/product proof | FR-025; NFR-011~012, 024~027 | labels, contrast, two-cue focus and identical reducer commands; separate host/build/package/Common Emulator/Aurum/TV evidence; only Browser-owned delivery paths. |

## Agent contract

### Entity and Browser category

The live `Tizen.Entity.Browser` resolver declares stable `Id`, category `Tizen.Action.Browser`, and `Tv_Tizen.Action.Browser_GetBrowserByIds`. Public Entity fields are `Url`, `Title`, and `Details`; each must be a bounded redacted projection of the current visible normal-mode page.

Generate the entire `Tizen.Action.Browser` category in catalog order using `actionc -a Tizen.Action.Browser`. The current action contracts are:

| Action | Agent intent | success | bounded failure / postcondition |
|---|---|---|---|
| `Tv_Tizen.Action.Browser_GetCurrent` | obtain current public page | `Status` + Browser Entity | typed `not_found` when no current visible page. |
| `Tv_Tizen.Action.Browser_Go` | navigate current selected tab | typed success queues same use-case path as UI | reject non-HTTP(S), credentials, and over-limit input; verify through `GetCurrent` or resolver. |
| `Tv_Tizen.Action.Browser_ToCalendar` | request calendar conversion | only a supported typed conversion | current scope returns initialized typed `unavailable`; no scraping/invented event. |
| `Tv_Tizen.Action.Browser_ToPresentation` | represent current visible page | initialized `Status` + Presentation | reject non-current/hidden/transient Entity; only current snapshot drives output. |
| `Tv_Tizen.Action.Browser_GetBrowserByIds` | resolve stable page IDs | requested order and duplicates preserved | IDs: 1~50, each ≤256; unresolved IDs explicitly returned. |

`details.appid = org.tizen.next-browser` in platform schemas is not Browser's sample identity. If the sample advertises a method, its manifest registration and target probe must select `appid = org.tizen.browser` explicitly.

### Current ViewAnnotation category

The current category is **`Tizen.Action.View`**. Its public action names are exactly:

1. `Common_Tizen.Action.View_FindById`
2. `Common_Tizen.Action.View_GetAnnotatedViews`
3. `Common_Tizen.Action.View_GetFocusedView`
4. `Common_Tizen.Action.View_ToPresentation`

Generate the entire category with `actionc -a Tizen.Action.View`; retain output byte-for-byte. The View provider owns a lock-protected immutable registry of currently visible Browser views. A published View has a stable per-surface View ID, actual focus identity, positive finite `ScreenBounds`/`WindowBounds`, and nested `Annotation` with `EntityType = Tizen.Entity.Browser`, stable `EntityId`, and `EntityInfo =` generated Browser Entity `ToJson()` output. Bounds belong to the enclosing View, not Annotation. Hidden Home/loading/error/Tabs/modal/paused/terminated surfaces clear or suppress the Browser page snapshot rather than publishing stale context.

`FindById`, `GetAnnotatedViews`, `GetFocusedView`, and `View_ToPresentation` must use that same registry. Failure returns a fully initialized serializer-safe output graph; a caller-forged annotation must not be trusted.

### A2UI and Presentation boundary

Canonical producer semantics are Google A2UI v0.9.1, Basic Catalog URL `https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json`, using a declared `createSurface → updateComponents → updateDataModel → deleteSurface` lifecycle. The checked contract source is `a2ui-project/a2ui` revision `ec97cb0d7499932e67003ffe5b709a3db7e7033a` (2026-08-07; inspected 2026-08-09), where v0.9.1 is Current Production and v1.0 is Candidate. Browser supports only bounded `Column`/`Text` semantics and data; renderer styling/focus/layout remain renderer-controlled.

The current generated `Tizen.Entity.Presentation` ABI has only `Template` and `Document` strings. Its split `surfaceUpdate` / `dataModelUpdate` pair is named legacy v0.8 compatibility output, not v0.9.1. `ToPresentation` and `View_ToPresentation` may return that explicitly named legacy adapter from the same redacted current snapshot, but must not mislabel it canonical. Canonical Action-to-Display target transport remains blocked unless a separately negotiated transport is introduced outside this Browser-only scope.

## Verification matrix and stop conditions

| Gate | Must prove | status now |
|---|---|---|
| contract/HTML | executable canonical UI state and input model | not re-verified in clean cycle; next slice |
| host | RED→GREEN portable Domain/UseCases/Persistence/App policy tests | historical-only; clean core slice required |
| generated provenance/build | fresh whole-category Browser/View byte comparison and provider/App compilation | historical-only; fresh generation required |
| TPK/signing | real Tizen build, archive payload/signatures, selected signing mode | historical-only; fresh package required |
| Common Emulator Action/View | discovery, explicit sample appid RPC success/negative, postconditions, liveness | historical-only; fresh unmodified generation may block this gate |
| Common Emulator WebView/Aurum | real HTTPS navigation, input/focus/lifecycle and decoded screenshots | historical-only; fresh package required |
| Display/A2UI | legacy round trips and separately stated canonical transport limit | not complete; canonical target transport is outside Browser-only scope |
| TV/product | target-specific behavior/signing | unverified; never implied by Common Emulator evidence |

Stop rather than patch around: a missing target WebView capability; Tizen package/signing/transport failure; protected-path requirement; or a fresh unmodified generated binding that reaches a target RPCPort ABI failure (including missing `StubBase.HasPrivilegeLocal`). The latter is a framework generator/runtime blocker: preserve direct generation provenance and target error, do not modify generated C#.

## Slice handoff

`BROWSER-HTML-003` is the next smallest dependency-complete slice: browser-verify the existing canonical `refs/one-ui-sample.html`, validate its UI state/input contract against this document, and record only HTML evidence. It must not modify NUI implementation, generated bindings, Calendar, Reminder, schemas, or target state.
