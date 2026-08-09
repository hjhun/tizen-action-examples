# Browser 요구사항 추적성

갱신일: 2026-08-09 (Stage 0)
상태 정의: `문서화`는 구현 완료가 아니며, `부분`은 일부 source/host evidence만 존재하고 target gate가 남았다는 뜻이다.

## 기능 요구사항

| 요구사항 | 모듈/아키텍처 | 현재/예정 source | host test | target test | screenshot/evidence | 상태 |
|---|---|---|---|---|---|---|
| FR-BROWSER-001 | 2A App shell | `BrowserApplication`, `BrowserChromeView` | App geometry/focus tests 예정 | cold launch + initial focus | `native-browser-address-focus-1920x1080.png` | 부분 |
| FR-BROWSER-002 | 2B navigation reducer/runtime | `BrowserNavigationCoordinator`, `NuiWebViewRuntime` | `Browser.UseCases.Tests` cancellation/validation 확장 | real HTTPS submit/loading | loading HTML/native 예정 | 부분 |
| FR-BROWSER-003 | 2B input normalization | search normalizer 예정 | URL/search/bounds table tests 예정 | keyboard submit | search HTML/native 예정 | 미구현 |
| FR-BROWSER-004 | 2B loaded state | coordinator + page snapshot | success/stale tests 존재·확장 | real WebView HTTPS completion | page HTML/native 예정 | 부분 |
| FR-BROWSER-005 | 2B reload | runtime/reducer command 예정 | single-intent reload test 예정 | Reload key/pointer | page reload frame/trace 예정 | 부분 |
| FR-BROWSER-006 | 2B recovery | recovery state/view 예정 | offline/retry reducer tests 예정 | offline/error/retry | 기존 `html-browser-offline-1264x625.png`는 HTML-only | 미구현(NUI) |
| FR-BROWSER-007 | 2B engine unavailable | runtime capability/error state 예정 | startup exception test 예정 | unavailable engine probe | engine-error HTML/native 예정 | 미구현 |
| FR-BROWSER-008 | 2B timeout | 15초 timeout policy로 수정 예정 | fake runtime/clock test 예정 | target timeout 또는 controlled unreachable | timeout HTML/native 예정 | 미구현 |
| FR-BROWSER-009 | 2B stale suppression | `BrowserNavigationCoordinator` | delayed A/B stale test 존재·확장 | rapid consecutive Go/input | trace + final native frame 예정 | 부분 |
| FR-BROWSER-010 | 2B history | WebView history adapter 예정 | availability/reducer tests 예정 | Back/Forward success+disabled | focused chrome frames 예정 | 미구현 |
| FR-BROWSER-011 | 2B/2C Back hierarchy | shared reducer 예정 | modal/tabs/history table tests 예정 | remote Back each state | error/tabs/modal native frames 예정 | 미구현 |
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
| FR-BROWSER-026 | 2B/2D state consistency | shared immutable snapshot 예정 | state projection matrix 예정 | transient Action/View calls | loading/error source/render pair | 미구현 |

## 품질 요구사항

| 요구사항 | 아키텍처/source | host 검증 | target/evidence | 상태 |
|---|---|---|---|---|
| NFR-BROWSER-001 | App shell-first startup | startup state test 예정 | cold launch timing + initial frame 예정 | 미측정 |
| NFR-BROWSER-002 | shared input reducer | reducer latency/single-command test 예정 | key/pointer frame timing 예정 | 미측정 |
| NFR-BROWSER-003 | immediate loading/recovery render | reducer transition test 예정 | target frame timing 예정 | 미측정 |
| NFR-BROWSER-004 | coordinator cancellation/stale ID | delayed runtime tests 일부 존재 | rapid target navigation 예정 | host 부분 |
| NFR-BROWSER-005 | bounded timeout | runtime timeout 현재 2분, 15초로 교정 예정 | controlled timeout 예정 | 불일치 |
| NFR-BROWSER-006 | async gates/bounded queues | use-case concurrency tests 일부 존재 | target responsiveness 예정 | 부분 |
| NFR-BROWSER-007 | domain/session bounds | page/session/resolver tests 일부 존재 | oversized Action negative 예정 | 부분 |
| NFR-BROWSER-008 | versioned atomic persistence | serializer tests 존재, file adapter 없음 | restart/corruption 예정 | 부분 |
| NFR-BROWSER-009 | public metadata projection | serialization/View source review | screenshot/report/A2UI scan 예정 | 부분 |
| NFR-BROWSER-010 | HTTP(S)/no auto approval | URL validation tests 일부 | real HTTPS + permission denial 예정 | 부분 |
| NFR-BROWSER-011 | accessible labels | HTML/source assertion 예정 | Aurum tree 또는 capability-limit 기록 | 부분 |
| NFR-BROWSER-012 | contrast/two focus cues | token contrast script 예정 | screenshot visual review | two cues 일부 native 증거 |
| NFR-BROWSER-013 | focus graph/trap/restore | reducer table tests 예정 | all-state key verification | command band만 부분 |
| NFR-BROWSER-014 | input parity | HTML suite/reducer tests 예정 | key/pointer/touch matrix | 미검증 |
| NFR-BROWSER-015 | uniform canvas | viewport helper/tests 예정; current inline source | multi-resolution native는 범위 가능성 따라 | source 부분 |
| NFR-BROWSER-016 | insets/invalid retention | geometry tests 예정; current resize source | non-zero inset target 예정 | source 부분 |
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
