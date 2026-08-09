# Browser 현재 디자인 감사

검토일: 2026-08-09
판정: **제품 계약 미충족 — Stage 1/2 교정 필수**

현재 Browser는 real `Tizen.NUI.BaseComponents.WebView`, inset-aware 1920×1080 canvas, 주소/Reload/Tabs command band, 두 cue focus의 유효한 첫 vertical slice를 보유한다. 그러나 Samsung Browser-derived 완결 제품으로 승인할 수 없다. HTML에는 일부 상태가 있으나 네이티브와 reducer가 분리되어 있고, loading/recovery/tabs/modal이 NUI에 없으며, current-state canonical A2UI와 대상 RPC/round trip 증거가 없다.

## 항목별 감사

| 항목 | 현재 상태 | Samsung Browser/Tizen 계약과의 차이 | 판정 |
|---|---|---|---|
| 정보 구조 | command band + page context + real WebView | 큰 WebView와 별도 Tabs 정신 모델은 맞지만 Home/loading/error/tabs가 하나의 state machine이 아님 | 부분 |
| 주소/검색 | absolute URL만 NUI에서 허용; HTML은 검색어 변환 | visible 계약과 production behavior 불일치 | 실패 |
| 탐색 제어 | Back/Forward가 영구 disabled, Reload만 동작 | 실제 history availability와 disabled state 전환 없음 | 실패 |
| loading | HTML progress만 존재 | NUI `PageLoadStarted`가 UI state로 노출되지 않음 | 실패 |
| 오류/회복 | NUI는 title/url text만 바꿈 | offline/engine-error/timeout surface, Retry/Back, focus trap/restore 없음 | 실패 |
| Tabs | HTML local array만 구현, NUI button no-op | shared bounded tab model, select/new/close/persistence 없음 | 실패 |
| 확인 dialog | HTML close dialog만 존재 | NUI modal/trap/Back/restore 없음 | 실패 |
| Back | HTML Escape 일부 구현 | NUI modal/tabs/history/app hierarchy 없음 | 실패 |
| D-pad/keyboard | command band Left/Right와 WebView Up/Down 일부 | tabs/modal/error graph와 disabled skip 전체가 없음 | 부분 |
| pointer/touch | command control handler 존재 | address/tabs/modal 모든 path와 actual target activation 증거 없음 | 부분 |
| typography/spacing/shape | 밝은 neutral, restrained blue, 큰 content region | 기존 native frame은 visual side-by-side 평가와 non-zero inset 증거 부족 | 부분 |
| privacy | public metadata projection과 body 제외 방향 존재 | URL query/fragment redaction, error/log/report scanning, Secret-mode 비지원 표시 부족 | 부분 |
| Entity/Action | generated whole category, thin adapter, host contract 일부 | `ToCalendar` unsupported, target positive/negative RPC 차단 | 부분/차단 |
| ViewAnnotation | source에서 measured bounds와 generated `ToJson()` 사용 | provider runtime 차단, focus가 WebView일 때만 true, transient UI 동기화 미검증 | 차단 |
| A2UI | legacy-like Template/Document JSON 일부 | canonical v0.9.1 lifecycle/catalog 없음, DisplayPresentation round trip 없음 | 실패/차단 |
| 증거 | HTML 3장, native 3장 | loading/error/tabs/modal/real HTTPS/pointer/A2UI 증거 없음 | 실패 |

## 교정 순서

1. Stage 1: canonical HTML을 실제 state machine으로 정리하고 primary/exception/input/modal/responsive flow를 browser-verify한다.
2. Stage 2A: NUI chrome과 geometry를 테스트 가능한 viewport/focus model로 고정하고 real WebView region만 유지한다.
3. Stage 2B: UI/provider가 공유하는 reducer/navigation state, cancellation, timeout, stale suppression, offline/retry/Back을 구현한다.
4. Stage 2C: bounded tab aggregate, persistence, select/new/close confirmation, modal focus restoration을 구현한다.
5. Stage 2D: 같은 visible snapshot에서 Entity/View/canonical A2UI를 만들고 legacy v0.8 compatibility를 명시적으로 분리한다.
6. Stage 3: host/build/package/install/real HTTPS/Action/View/A2UI/Aurum/parity gate를 독립적으로 실행한다.

## Stage 0 설계 대안 결론

- 선택: **상단 compact command band + content-first real WebView + 별도 Tabs manager**. Android Samsung Browser의 page-first 정보 구조를 TV focus에 가장 적은 변형으로 옮긴다.
- 기각: desktop multi-tab strip. 탭이 페이지 위에 지속 경쟁하고 D-pad target이 과밀해진다.
- 기각: quick-access/card dashboard. 실제 웹 탐색보다 fixture card가 제품처럼 보이는 위험이 있다.

이 판정은 사용자 승인 주장이 아니다. 사용자의 명시적 autonomous mission에 따라 교정 단계를 계속한다.
