# Browser 요구사항 추적성

갱신일: 2026-08-09 (Stage 3 final-partial)
상태 정의: `문서화`는 구현 완료가 아니며, `부분`은 일부 source/host evidence만 존재하고 target gate가 남았다는 뜻이다.

## 기능 요구사항

| 요구사항 | 모듈/아키텍처 | 현재/예정 source | host test | target test | screenshot/evidence | 상태 |
|---|---|---|---|---|---|---|
| FR-BROWSER-001 | 2A App shell | `BrowserApplication`, `BrowserChromeView`, `BrowserShellContract` | physical-root/canvas geometry + focus graph PASS | Common cold launch/Home/address focus PASS | `native-browser-home-stage3-1920x1080.png` | Common target PASS |
| FR-BROWSER-002 | 2B navigation reducer/runtime | `BrowserNavigationCoordinator`, `NuiWebViewRuntime` | input→Loading→typed result PASS | real HTTPS submit/loading PASS | native loading/page | Common target PASS |
| FR-BROWSER-003 | 2B input normalization | `BrowserNavigationInput` | URL/search/empty/512·4096 bounds/credential rejection/query redaction PASS | OSK blank submit → InvalidInput PASS | native invalid-input | bounded negative target PASS; search target 미검증 |
| FR-BROWSER-004 | 2B loaded state | coordinator immutable state + public `BrowserPage` | loaded/latest state + public URI PASS | real WebView HTTPS completion PASS | native page | Common target PASS |
| FR-BROWSER-005 | 2B reload | shared coordinator/runtime command | navigate→reload single pipeline PASS | Reload key/pointer | reload native frame/trace 예정 | host PASS/target 대기 |
| FR-BROWSER-006 | 2B recovery | typed state + NUI recovery surface | offline mapping, Retry/Back restoration, 256-char error bound PASS | InvalidInput/Retry PASS; offline capture blocked | native invalid-input; blocker log | 부분 — offline target 차단 |
| FR-BROWSER-007 | 2B engine unavailable | `UnavailableWebRuntime` + engine-error surface | typed engine mapping/visual state PASS | unavailable engine probe | engine-error native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-008 | 2B timeout | 15초 policy + runtime timeout/`StopLoading` | exact 15초 policy + typed timeout mapping PASS | controlled target timeout | timeout HTML; native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-009 | 2B stale suppression | active linked cancellation + monotonic intent | A cancellation 관찰 후 B만 publish PASS | rapid consecutive Go/input | trace + final native frame 예정 | host PASS/target 대기 |
| FR-BROWSER-010 | 2B history | real `WebView.GoBack/GoForward/CanGo*` adapter | availability/one-step command pipeline + disabled skip PASS | Back/Forward success+disabled | focused chrome native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-011 | 2B/2C Back hierarchy | error→stable/home, page→history; tabs/modal은 2C | recovery Back + history host PASS | remote Back each state | error/tabs/modal native 예정 | 2B host PASS/2C·target 대기 |
| FR-BROWSER-012 | 2C tabs aggregate | `BrowserTabWorkspace`, coordinator, NUI clipped ordered rows | order/selected/max/surface tests PASS | open Tabs/select PASS | native tabs | Common target PASS; max/scroll 미검증 |
| FR-BROWSER-013 | 2C new tab | stable GUID ID, Home tab, max-20 disabled, persist-first publish | unique/non-reuse/max/new-selected/focus/durable-before-publish PASS | New tab/Home PASS; max-20 target 미검증 | native Home/Tabs | 부분 target PASS |
| FR-BROWSER-014 | 2C select tab | tab coordinator + selected WebView/home activation | selected/order/shared-page snapshot PASS | select via key/pointer | selected tab native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-015 | 2C close request/modal | full-canvas modal + bounded/fallback title + Cancel initial | title/modal state/visual/focus PASS | Close → modal PASS | native close confirmation | Common target PASS |
| FR-BROWSER-016 | 2C confirm close | aggregate + atomic snapshot | exactly one/nearest/last-tab guard/ID non-reuse PASS | confirm 3→2 PASS | native post-close trace/frame | Common target PASS |
| FR-BROWSER-017 | 2C cancel/restore | immutable cancel + invoking close focus ID + Back hierarchy | order/selected unchanged + focus restoration PASS | modal trap/Back/restore PASS | native focus frames | Common target PASS |
| FR-BROWSER-018 | 2C persistence/lifecycle | session v2 tabs, v1 migration, atomic store, persist-first mutation, pause save | round-trip/migration/malformed/unknown/256KiB/stale save/durable-before-publish PASS | pause/terminate/relaunch | relaunch native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-019 | 2D Action current | atomic `BrowserAgentStateRegistry` + `BrowserActionService.GetCurrent` | visible/transient/Tabs/paused projection PASS; generated adapter build PASS | positive + `not_found`/`unavailable` RPC | RPC JSON/log excerpt 예정 | host PASS/target 차단 |
| FR-BROWSER-020 | 2D Action Go | `BrowserActionService.Go`, selected-tab target contract, NUI bridge UI-thread recheck | valid/invalid/hidden surface + caller ID ≠ selected tab ID PASS | positive + invalid scheme + postcondition | Action trace + final page frame | host/source PASS/target 차단 |
| FR-BROWSER-021 | 2D resolver | `BrowserPageCatalog`, `BrowserActionService.GetBrowserByIds` | duplicate/order host PASS | positive + oversized/invalid RPC | resolver output in evidence | host PASS/target 차단 |
| FR-BROWSER-022 | 2D calendar conversion | initialized output + typed unavailable/invalid provider path | source/build inspection PASS | unavailable + invalid RPC | Action trace 예정 | source PASS/target 차단 |
| FR-BROWSER-023 | 2D View | measured `BrowserVisibleViewRegistry`, generated `ToJson()` mapper, live snapshot validation | finite/incomplete bounds, find/focus/clear registry PASS; adapter build PASS | discovery/Find/focus/bounds/lifecycle | focused source native frame 예정 | host/source PASS/target 차단 |
| FR-BROWSER-024 | 2D A2UI | `CreatePresentations` canonical v0.9.1 producer + named legacy adapter | official 4-message schema PASS, current Display parser PASS, redaction/bounds PASS | two legacy DisplayPresentation round trips; canonical transport blocked | source/render native pair 예정 | host PASS/legacy target 대기/canonical target 차단 |
| FR-BROWSER-025 | 1/2A/2B/2C input | HTML reducer + NUI reducer/focus graph | HTML keyboard/pointer suite + reducer tests | remote/OSK/pointer click PASS; tap semantic 부분 | state별 native frame | Common 부분 PASS |
| FR-BROWSER-026 | 2B/2D state consistency | navigation/workspace/lifecycle → atomic agent state; same page → View/A2UI | visible/transient/Tabs/hidden/stale-tab suppression PASS | transient Action/View calls | loading/error source/render pair 예정 | host PASS/target 대기 |

## 품질 요구사항

| 요구사항 | 아키텍처/source | host 검증 | target/evidence | 상태 |
|---|---|---|---|---|
| NFR-BROWSER-001 | App shell-first startup | startup state test 예정 | cold launch timing + initial frame 예정 | 미측정 |
| NFR-BROWSER-002 | UI/Action shared coordinator | single command pipeline host PASS | key/pointer frame timing 예정 | host PASS/latency target 대기 |
| NFR-BROWSER-003 | synchronous Loading publish + visual-state mapping | loading/recovery transition host PASS | target ≤100/500ms timing 예정 | host PASS/timing 대기 |
| NFR-BROWSER-004 | active CTS + stale ID | second intent의 first cancellation 관찰과 stale suppression PASS | rapid target navigation 예정 | host PASS/target 대기 |
| NFR-BROWSER-005 | `BrowserNavigationPolicy` + runtime timeout | exact 15초와 typed timeout/Retry mapping PASS | controlled timeout 예정 | host/source PASS/target 대기 |
| NFR-BROWSER-006 | coordinator/runtime async gates, previous request cancellation | serial runtime command + latest publish PASS | target responsiveness 예정 | host PASS/target 대기 |
| NFR-BROWSER-007 | domain/session/error bounds | max-20, 80-char dialog, 256KiB store, page/resolver bounds PASS | oversized Action negative 예정 | host PASS/target·2D 대기 |
| NFR-BROWSER-008 | versioned atomic persistence | v2 tabs, v1 migration, same-directory temp replace, malformed fail-closed PASS | restart/corruption 예정 | host PASS/target 대기 |
| NFR-BROWSER-009 | query/fragment/userinfo-free public URI + generic bounded engine errors | projection/redaction/credential tests PASS | screenshot/report/A2UI scan 예정 | host PASS/target·2D 대기 |
| NFR-BROWSER-010 | HTTP(S), credential rejection, no approval path | URL/search/scheme/credential validation PASS | real HTTPS + permission denial 예정 | host PASS/target 대기 |
| NFR-BROWSER-011 | accessible labels | HTML/source assertion 예정 | Aurum tree 또는 capability-limit 기록 | 부분 |
| NFR-BROWSER-012 | contrast/two focus cues | token contrast script 예정 | screenshot visual review | two cues 일부 native 증거 |
| NFR-BROWSER-013 | focus graph/trap/restore | shell + recovery + tab selected/new/close + modal Cancel/Close trap/restore contract PASS | Home/recovery/tab/modal key verification PASS | Common target PASS |
| NFR-BROWSER-014 | input parity | HTML suite/reducer tests PASS | remote/OSK/click PASS; tap semantic 부분 | 부분 |
| NFR-BROWSER-015 | uniform canvas | `ReferenceCanvasViewport` 1920×1080/16:9/4:3/ultrawide host matrix PASS | multi-resolution native는 범위 가능성 따라 | host PASS/target 대기 |
| NFR-BROWSER-016 | insets/invalid retention | non-zero inset + invalid/transient retention host matrix PASS, resize+inset event source 연결 | non-zero inset target 예정 | host PASS/target 대기 |
| NFR-BROWSER-017 | localization | string catalog/longest-string tests 예정 | ko/en frames 예정 | 미구현 |
| NFR-BROWSER-018 | pause clear/save, resume state/layout republish, terminate cancellation/unsubscribe | session stale save + source lifecycle review PASS | pause/terminate/relaunch | host/source PASS/target 대기 |
| NFR-BROWSER-019 | generated ABI | whole-category fresh C# generation byte-identical for Browser/View | installed runtime compatibility | host provenance PASS/runtime 차단 |
| NFR-BROWSER-020 | A2UI v0.9.1/legacy split | official envelope/catalog schema + actual legacy parser PASS | legacy DisplayPresentation target; canonical transport blocked | host PASS/target 분리 |
| NFR-BROWSER-021 | actual View bounds | finite positive/complete window geometry + find/focus/clear registry PASS | RPC + Aurum | host PASS/target 차단 |
| NFR-BROWSER-022 | bounded/redacted logs | log projection tests 예정 | target log scan 예정 | 미구현 |
| NFR-BROWSER-023 | acceptance completeness | 이 ledger completeness script 예정 | 모든 target gate | Stage 0 문서화 |
| NFR-BROWSER-024 | separate build gates | clean host build/test와 Tizen build를 분리 실행 | emulator-test-only package/install 별도 PASS | PASS |
| NFR-BROWSER-025 | profile claim boundary | docs claim scan PASS | Common only; TV/production signing 미검증 명시 | PASS |
| NFR-BROWSER-026 | screenshot validity | PNG decode/dimension/non-blank/privacy scan | native 6개 1920×1080 RGB | Common evidence PASS |
| NFR-BROWSER-027 | exact-path delivery | staged allowlist/lock | commit/push output | Stage별 적용 예정 |

## Stage 0 문서 gate

| 산출물 | 검증 | 상태 |
|---|---|---|
| `PRODUCT_REQUIREMENTS.md` | stable ID, 필수 field, 범위 밖 scan | 작성 완료, 검증 예정 |
| `QUALITY_REQUIREMENTS.md` | stable ID, 수치 threshold, gate boundary scan | 작성 완료, 검증 예정 |
| `SAMSUNG_ANDROID_UI_REFERENCE.md` | official URL, app/screen/version/date, direct/adaptation 구분 | 작성 완료, 검증 예정 |
| `TRACEABILITY.md` | FR/NFR ID 누락·중복 검사 | 작성 완료, 검증 예정 |
| `DESIGN_REVIEW.md` | explicit verdict/gap/correction plan | 작성 완료, 검증 예정 |

## Stage 1 HTML contract gate

| 요구사항 | HTML 증거 | 결과 | production 경계 |
|---|---|---|---|
| FR-BROWSER-001, 003 | home initial focus, search normalization | PASS | NUI search/home은 Stage 2 |
| FR-BROWSER-002, 004, 005 | submit/loading/page/reload semantic state | PASS | real WebView completion은 Stage 2/3 |
| FR-BROWSER-006~009 | offline/engine-error/timeout/retry/stale intent fixture | PASS | NUI/runtime test는 Stage 2B |
| FR-BROWSER-010, 011 | history disabled state와 Back hierarchy | PASS | real WebView history는 Stage 2B |
| FR-BROWSER-012~017 | 1~20 tabs, select/new/close, confirm/cancel/trap/restore | PASS | domain/persistence/NUI는 Stage 2C |
| FR-BROWSER-025 | D-pad/keyboard/pointer/touch command parity | PASS | installed input parity는 Stage 3 |
| NFR-BROWSER-011~014 | labels, two-cue focus, graph/trap/input | PASS(HTML) | Aurum tree/input은 Stage 3 |
| NFR-BROWSER-015 | four-shape centered uniform transform | PASS(HTML) | NUI inset/resize는 Stage 2A/3 |
| NFR-BROWSER-026 | current HTML PNG 6개 decode/dimension/non-blank/privacy review | PASS(HTML) | native image set은 Stage 3 |

Playwright Chromium은 direct `file:` open에서 primary/exception flow, scrolled 10th tab focus, modal confirm/cancel, max-tab disabled, pointer/touch를 실행했고 console/page error는 0이었다. 외부 request는 없었다. 이 결과는 HTML-only이며 FR-BROWSER-019~024 target Action/Entity/View/A2UI 상태를 변경하지 않는다.

## Stage 2A NUI shell/chrome/scaling gate

| 계약 | source/host 증거 | 결과 | 남은 target 경계 |
|---|---|---|---|
| FR-BROWSER-001 | full-window physical root, 1920×1080 canvas, 132/92/6 shell, 1816×806 content-only WebView | PASS | current installed launch/frame |
| FR-BROWSER-010, 025 | Back/Forward disabled-skip, Reload→Address→Tabs, command↔WebView vertical graph | PASS | WebView actual history와 native remote/pointer |
| NFR-BROWSER-015 | four aspect-ratio centered-uniform viewport table | PASS | target resize/multiple mode |
| NFR-BROWSER-016 | non-zero insets와 invalid/exhausted/NaN rejection, `Resized`+`InsetsChanged` 연결 | PASS | target non-zero inset와 last-valid frame |

`Browser.App.Tests`의 RED compile failure를 확인한 뒤 최소 계약 타입과 NUI 연결을 추가했다. Domain, Persistence, UseCases, ActionProvider, App 실행형 host test 5개와 전체 solution build가 통과했다. 이 gate는 package, Common Emulator, native screenshot, Action/View/A2UI를 증명하지 않는다.

## Stage 2B navigation/runtime state gate

| 계약 | source/host 증거 | 결과 | 남은 target 경계 |
|---|---|---|---|
| FR-BROWSER-002~005 | URL/search normalization, immediate Loading, loaded public page, Reload | PASS | real HTTPS/keyboard/pointer |
| FR-BROWSER-006~008 | Offline/EngineError/Timeout/InvalidInput, Retry/Back/Edit address, exact 15초 | PASS | controlled target error/timeout |
| FR-BROWSER-009 | newer intent cancels active token before serialized next runtime operation; stale A never publishes | PASS | rapid real WebView navigation |
| FR-BROWSER-010~011 | WebView Reload/GoBack/GoForward/CanGo* adapter, disabled focus skip, recovery→stable/home Back | PASS | installed remote and actual history |
| NFR-BROWSER-007, 009, 010 | input/error bounds, HTTP(S), embedded credential rejection, query/fragment-free public projection | PASS | target log/screenshot/Entity scan |

`Browser.UseCases.Tests`는 의도한 RED compile failure 뒤 input/state/cancellation/history/recovery 계약을 통과했고, `Browser.App.Tests`는 loading/recovery visual mapping과 Retry→Back→Edit address focus trap을 통과했다. 전체 solution build는 성공했다. WebView network, installed visuals, package, RPC, View/A2UI는 아직 target gate다.

## Stage 2C tabs/confirmation/persistence gate

| 계약 | source/host 증거 | 결과 | 남은 target 경계 |
|---|---|---|---|
| FR-BROWSER-012~014 | ordered 1~20 tab workspace, stable non-reused ID, selected Home/page, clipped NUI list/New tab | PASS | native D-pad/pointer/scroll |
| FR-BROWSER-015~017 | individual close modal, 80-char title, Cancel initial, Cancel/Back restore, exact-one close, nearest remaining | PASS | native modal trap/frame |
| FR-BROWSER-018 | session v2 nullable-page tabs, v1 migration, selected/order/ID restore, 256KiB atomic store, persist-first mutations, malformed→Home | PASS | installed pause/relaunch/corruption |
| NFR-BROWSER-013 | modal full-canvas input boundary and Cancel↔Close trap; invoking close/selected card focus IDs | PASS | Aurum focus/input proof |
| NFR-BROWSER-018 | pause clears View and saves; resume reapplies geometry/state; terminate cancels/unsubscribes | PASS(source) | target lifecycle proof |

Domain/Persistence/UseCases/App RED→GREEN tests와 실행형 host test 5개, clean solution build가 통과했다. 이 gate는 installed tabs, persisted relaunch, native modal, package, RPC, View/A2UI를 증명하지 않는다.

## Stage 2D Entity/View/A2UI gate

| 계약 | source/host 증거 | 결과 | 남은 target 경계 |
|---|---|---|---|
| FR-BROWSER-019~022 | one atomic visible-state query, current-only Presentation, bounded provider input, ordered duplicate resolver, initialized failure output | portable contract/state tests + generated adapter build PASS | installed typed Action RPC와 resolver postcondition |
| FR-BROWSER-023 | stable `browser:page:<id>`, generated Browser `ToJson()`, finite positive screen/window bounds, actual focus, find/focus/clear registry, forged annotation rejection | pure registry tests + source mapper/build PASS | installed provider discovery, measured RPC values, lifecycle/focus 변화 |
| FR-BROWSER-024 | official v0.9.1 create/components/data/delete + Basic Catalog; separately named legacy Display adapter | 4 official schema validations + current Display parser semantic-tree PASS | 두 legacy RPC→Display native round trip; canonical target transport blocker |
| FR-BROWSER-026 | navigation + selected tab + workspace + lifecycle의 한 atomic projection | Home/Page/Loading/Tabs/hidden/stale-selected mismatch suppression PASS | 전환 중 installed Action/View query |
| NFR-BROWSER-009, 020 | query/fragment redaction, 256-char display fields, total 256KiB budget, version/profile non-mixing | private marker/bounds/schema/parser tests PASS | installed payload/log/privacy scan |
| NFR-BROWSER-019 | whole-category generator provenance | fresh Browser 5-action/View 4-action C# output SHA-256가 tracked source와 byte-identical | installed `StubBase.HasPrivilegeLocal` compatibility |

generated service 객체는 Tizen reference assembly가 host 실행 구현을 제공하지 않아 desktop process에서 직접 생성할 수 없었다. 이 시도를 PASS로 세지 않았다. portable contracts와 current Display parser는 host에서 실행했고 generated adapters는 compile했으며, 실제 dispatch/status/DTO wire는 Stage 3 Common Emulator RPC gate로 남겼다. 현재 두 문자열 `Tizen.Entity.Presentation`과 Display parser에는 ordered canonical v0.9.1 transport가 없으므로 canonical target render는 Browser-only 범위에서 해소할 수 없다. 자세한 경계는 [`A2UI_CONTRACT.md`](A2UI_CONTRACT.md)에 기록했다.

## Stage 3 Common Emulator gate

패키지, target, screenshot provenance와 blocker 상세는 [`STAGE3_VALIDATION.md`](STAGE3_VALIDATION.md)에 있다. clean host test/build, Tizen build, emulator-only signing/package, archive payload, install/launch, real HTTPS WebView, Home/Loading/Page/InvalidInput/Tabs/modal native states는 독립 PASS다. Browser/View provider discovery는 PASS지만 exact Browser RPC가 generated/runtime ABI mismatch로 앱을 종료하므로 FR-BROWSER-019~024의 target RPC/View/legacy Display gate는 차단 상태를 유지한다. canonical A2UI target transport와 offline capture도 각각 독립 차단 상태다.
