# Browser 요구사항 추적성

갱신일: 2026-08-09 (Stage 2B)
상태 정의: `문서화`는 구현 완료가 아니며, `부분`은 일부 source/host evidence만 존재하고 target gate가 남았다는 뜻이다.

## 기능 요구사항

| 요구사항 | 모듈/아키텍처 | 현재/예정 source | host test | target test | screenshot/evidence | 상태 |
|---|---|---|---|---|---|---|
| FR-BROWSER-001 | 2A App shell | `BrowserApplication`, `BrowserChromeView`, `BrowserShellContract` | physical-root/canvas geometry + focus graph PASS | cold launch + initial focus | 과거 `native-browser-address-focus-1920x1080.png`; current native 예정 | host PASS/target 대기 |
| FR-BROWSER-002 | 2B navigation reducer/runtime | `BrowserNavigationCoordinator`, `NuiWebViewRuntime` | input→Loading→typed result PASS | real HTTPS submit/loading | Stage 1 loading HTML; native 예정 | host PASS/target 대기 |
| FR-BROWSER-003 | 2B input normalization | `BrowserNavigationInput` | URL/search/empty/512·4096 bounds/credential rejection/query redaction PASS | keyboard submit | Stage 1 search HTML; native 예정 | host PASS/target 대기 |
| FR-BROWSER-004 | 2B loaded state | coordinator immutable state + public `BrowserPage` | loaded/latest state + public URI PASS | real WebView HTTPS completion | page native 예정 | host PASS/target 대기 |
| FR-BROWSER-005 | 2B reload | shared coordinator/runtime command | navigate→reload single pipeline PASS | Reload key/pointer | reload native frame/trace 예정 | host PASS/target 대기 |
| FR-BROWSER-006 | 2B recovery | typed state + NUI recovery surface | offline mapping, Retry/Back restoration, 256-char error bound PASS | offline/error/retry | Stage 1 offline HTML; native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-007 | 2B engine unavailable | `UnavailableWebRuntime` + engine-error surface | typed engine mapping/visual state PASS | unavailable engine probe | engine-error native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-008 | 2B timeout | 15초 policy + runtime timeout/`StopLoading` | exact 15초 policy + typed timeout mapping PASS | controlled target timeout | timeout HTML; native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-009 | 2B stale suppression | active linked cancellation + monotonic intent | A cancellation 관찰 후 B만 publish PASS | rapid consecutive Go/input | trace + final native frame 예정 | host PASS/target 대기 |
| FR-BROWSER-010 | 2B history | real `WebView.GoBack/GoForward/CanGo*` adapter | availability/one-step command pipeline + disabled skip PASS | Back/Forward success+disabled | focused chrome native 예정 | host/source PASS/target 대기 |
| FR-BROWSER-011 | 2B/2C Back hierarchy | error→stable/home, page→history; tabs/modal은 2C | recovery Back + history host PASS | remote Back each state | error/tabs/modal native 예정 | 2B host PASS/2C·target 대기 |
| FR-BROWSER-012 | 2C tabs aggregate | tab domain/use case/view 예정 | order/selected/max tests 예정 | open Tabs/select | tabs HTML/native 예정 | HTML-only 부분 |
| FR-BROWSER-013 | 2C new tab | tab command/persistence 예정 | max-20/ID/focus tests 예정 | New tab/disabled at max | tabs-new HTML/native 예정 | HTML-only 부분 |
| FR-BROWSER-014 | 2C select tab | tab command + runtime binding 예정 | selection/order/state tests 예정 | select via key/pointer | selected tab HTML/native 예정 | HTML-only 부분 |
| FR-BROWSER-015 | 2C close request/modal | confirmation state 예정 | trap/title-bound tests 예정 | Close → modal | close-confirm HTML/native 예정 | HTML-only 부분 |
| FR-BROWSER-016 | 2C confirm close | aggregate + persistence 예정 | nearest selection/last-tab tests 예정 | confirm close | post-close HTML/native 예정 | HTML-only 부분 |
| FR-BROWSER-017 | 2C cancel/restore | focus restoration reducer 예정 | Cancel/Back invariant tests 예정 | modal Back/Cancel | restored-focus frame 예정 | HTML-only 부분 |
| FR-BROWSER-018 | 2C persistence/lifecycle | `BrowserSessionSnapshot`, `BrowserSessionCoordinator`, Tizen store 예정 | existing round-trip/version/stale save + failure injection 예정 | terminate/relaunch | relaunch state frame 예정 | 부분 |
| FR-BROWSER-019 | 2D Action current | `BrowserActionService.GetCurrent` | provider contract test 확장 | positive + `not_found` RPC | RPC JSON/log excerpt in evidence | host 부분/target 차단 |
| FR-BROWSER-020 | 2D Action Go | `BrowserActionService.Go`, NUI bridge | valid/invalid/unavailable tests 예정 | positive + invalid scheme + postcondition | Action trace + final page frame | host 부분/target 차단 |
| FR-BROWSER-021 | 2D resolver | `BrowserPageCatalog`, `BrowserActionService.GetBrowserByIds` | duplicate/order host PASS | positive + oversized/invalid RPC | resolver output in evidence | host PASS/target 차단 |
| FR-BROWSER-022 | 2D calendar conversion | current typed unavailable path | initialized output/invalid tests 예정 | unavailable + invalid RPC | Action trace | 부분/target 차단 |
| FR-BROWSER-023 | 2D View | `BrowserViewActionService`, NUI publish | view registry/mapping tests 예정 | discovery/Find/focus/bounds/lifecycle | focused source native frame | source 부분/target 차단 |
| FR-BROWSER-024 | 2D A2UI | canonical producer + legacy adapter 예정 | schema/catalog/equivalence/error tests 예정 | both DisplayPresentation round trips | source/render native pair | 미구현/target 차단 |
| FR-BROWSER-025 | 1/2A/2B/2C input | HTML reducer + NUI reducer/focus graph | HTML keyboard/pointer suite + reducer tests | remote/keyboard/pointer/touch | state별 HTML/native frame | 일부 command-band remote만 증명 |
| FR-BROWSER-026 | 2B/2D state consistency | shared immutable navigation state, page projection; A2UI는 2D | phase/public-page projection host PASS | transient Action/View calls | loading/error source/render pair | 2B host PASS/2D·target 대기 |

## 품질 요구사항

| 요구사항 | 아키텍처/source | host 검증 | target/evidence | 상태 |
|---|---|---|---|---|
| NFR-BROWSER-001 | App shell-first startup | startup state test 예정 | cold launch timing + initial frame 예정 | 미측정 |
| NFR-BROWSER-002 | UI/Action shared coordinator | single command pipeline host PASS | key/pointer frame timing 예정 | host PASS/latency target 대기 |
| NFR-BROWSER-003 | synchronous Loading publish + visual-state mapping | loading/recovery transition host PASS | target ≤100/500ms timing 예정 | host PASS/timing 대기 |
| NFR-BROWSER-004 | active CTS + stale ID | second intent의 first cancellation 관찰과 stale suppression PASS | rapid target navigation 예정 | host PASS/target 대기 |
| NFR-BROWSER-005 | `BrowserNavigationPolicy` + runtime timeout | exact 15초와 typed timeout/Retry mapping PASS | controlled timeout 예정 | host/source PASS/target 대기 |
| NFR-BROWSER-006 | coordinator/runtime async gates, previous request cancellation | serial runtime command + latest publish PASS | target responsiveness 예정 | host PASS/target 대기 |
| NFR-BROWSER-007 | domain/session bounds | page/session/resolver tests 일부 존재 | oversized Action negative 예정 | 부분 |
| NFR-BROWSER-008 | versioned atomic persistence | serializer tests 존재, file adapter 없음 | restart/corruption 예정 | 부분 |
| NFR-BROWSER-009 | query/fragment/userinfo-free public URI + generic bounded engine errors | projection/redaction/credential tests PASS | screenshot/report/A2UI scan 예정 | host PASS/target·2D 대기 |
| NFR-BROWSER-010 | HTTP(S), credential rejection, no approval path | URL/search/scheme/credential validation PASS | real HTTPS + permission denial 예정 | host PASS/target 대기 |
| NFR-BROWSER-011 | accessible labels | HTML/source assertion 예정 | Aurum tree 또는 capability-limit 기록 | 부분 |
| NFR-BROWSER-012 | contrast/two focus cues | token contrast script 예정 | screenshot visual review | two cues 일부 native 증거 |
| NFR-BROWSER-013 | focus graph/trap/restore | Stage 2A disabled-skip command↔WebView graph PASS; modal은 2C | all-state key verification | shell host PASS/target·modal 대기 |
| NFR-BROWSER-014 | input parity | HTML suite/reducer tests 예정 | key/pointer/touch matrix | 미검증 |
| NFR-BROWSER-015 | uniform canvas | `ReferenceCanvasViewport` 1920×1080/16:9/4:3/ultrawide host matrix PASS | multi-resolution native는 범위 가능성 따라 | host PASS/target 대기 |
| NFR-BROWSER-016 | insets/invalid retention | non-zero inset + invalid/transient retention host matrix PASS, resize+inset event source 연결 | non-zero inset target 예정 | host PASS/target 대기 |
| NFR-BROWSER-017 | localization | string catalog/longest-string tests 예정 | ko/en frames 예정 | 미구현 |
| NFR-BROWSER-018 | lifecycle cleanup | cancellation tests 일부; App late-callback tests 예정 | pause/terminate/relaunch | 부분 |
| NFR-BROWSER-019 | generated ABI | fresh generation/order/byte compare Stage 2D/3 | installed runtime compatibility | 기존 source provenance, runtime 차단 |
| NFR-BROWSER-020 | A2UI v0.9.1/legacy split | schema/profile tests 예정 | DisplayPresentation target | 미구현 |
| NFR-BROWSER-021 | actual View bounds | mapper/registry tests 예정 | RPC + Aurum | source 부분/target 차단 |
| NFR-BROWSER-022 | bounded/redacted logs | log projection tests 예정 | target log scan 예정 | 미구현 |
| NFR-BROWSER-023 | acceptance completeness | 이 ledger completeness script 예정 | 모든 target gate | Stage 0 문서화 |
| NFR-BROWSER-024 | separate build gates | Stage 3 commands | package/install separately | 기존 과거 증거만 있음 |
| NFR-BROWSER-025 | profile claim boundary | docs claim scan 예정 | Common only; TV not verified | 문서화 |
| NFR-BROWSER-026 | screenshot validity | Pillow/dimension/content scan | HTML/native image set | 기존 images decode, coverage 부족 |
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
