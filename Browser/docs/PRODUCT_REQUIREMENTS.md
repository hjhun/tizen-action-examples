# Browser 제품 요구사항

검토일: 2026-08-09
제품 범위: `org.tizen.browser` normal-mode Browser
기준 화면: 1920×1080 Tizen Common/TV reference canvas

이 문서는 Stage 0의 관찰 가능한 제품 계약이다. Samsung Browser의 주소/탐색/탭 정신 모델을 Tizen D-pad, 키보드, 포인터, 터치에 맞게 번역한다. Secret mode, 계정, 동기화, 다운로드, 확장, Galaxy AI, 북마크/방문 기록 UI는 이번 범위 밖이며 화면·저장·Action에 노출하지 않는다.

## 기능 요구사항

| ID | 우선순위 | 시나리오·사전조건 | 상태 전이 | 출력·오류 | Action / Entity / View / A2UI 영향 | 개인정보 보호 | 측정 가능한 수용 기준 |
|---|---|---|---|---|---|---|---|
| FR-BROWSER-001 | P0 | 앱 최초 실행. 저장된 유효 normal session이 없거나 복원이 실패함 | `launching → home` | 편집 가능한 주소/검색, local start content, tab count `1` | 성공한 실제 페이지가 생기기 전 `GetCurrent`는 typed `not_found`; stale View 없음 | 원격 콘텐츠·계정·방문 기록 fixture를 사용하지 않음 | 1920×1080에서 2초 이내 셸이 보이고 초기 포커스가 주소 필드에 있음 |
| FR-BROWSER-002 | P0 | 주소 필드에 완전한 `http://` 또는 `https://` URL 입력 | `home/page/error → loading` | 정규화된 URL, 진행 표시 | `Go`와 UI가 동일 `BrowserNavigationCoordinator`를 사용 | 최대 4096자, 인증정보 포함 URL은 persistence/annotation에서 제거 또는 거부 | Enter 후 100ms 이내 loading 시각 상태가 시작되고 UI thread가 차단되지 않음 |
| FR-BROWSER-003 | P0 | 주소 필드에 URL이 아닌 검색어 입력 | `home/page → loading` | 고정된 privacy-safe HTTPS 검색 URL로 변환 | local command이며 완료 후 현재 `Tizen.Entity.Browser` 갱신 | 검색어 최대 512자; fixture/log/report에 실제 사용자 입력을 남기지 않음 | 공백-only는 실행하지 않고 피드백, 유효 검색어는 percent-encoding되어 1회 탐색 |
| FR-BROWSER-004 | P0 | WebView가 HTTPS 페이지를 성공적으로 완료 | `loading → page` | 실제 WebView 콘텐츠, bounded title/URL, 진행 표시 종료 | stable page ID를 유지하며 `GetCurrent`, resolver, View, 두 Presentation 경로가 같은 snapshot 사용 | body/cookie/form/password는 Entity·View·A2UI에 포함하지 않음 | 성공 후 title ≤512, URL ≤4096, details ≤2048이며 이전 실패 overlay가 남지 않음 |
| FR-BROWSER-005 | P0 | 활성 페이지가 있고 Reload 선택 | `page/error → loading → page/error` | 같은 탭/ID에서 재시도 결과 | public Action 추가 없이 local command; 최신 결과만 publish | 입력과 공개 metadata만 사용 | Reload가 정확히 한 탐색을 만들고 superseded 결과가 화면을 덮지 않음 |
| FR-BROWSER-006 | P0 | WebView가 load error/offline을 보고 | `loading → offline` | 짧은 원인, Retry, Back | 실패는 current successful Entity를 임의 교체하지 않으며 A2UI state에는 bounded error code만 포함 | 원본 engine 메시지는 256자로 제한하고 URL query/fragment를 보고서에 남기지 않음 | 오류 화면이 500ms 이내 표시되고 Retry와 Back 중 하나에 포커스가 있음 |
| FR-BROWSER-007 | P0 | WebView 초기화/엔진 시작이 실패 | `launching/loading → engine-error` | 엔진 사용 불가 안내, Retry, Back | `Go`는 typed `unavailable`; invisible/stale View 제거 | stack trace·local path·engine 내부 정보 미노출 | 앱이 종료되지 않고 복구 제어가 활성화되며 오류 문자열 ≤256자 |
| FR-BROWSER-008 | P0 | 탐색이 timeout 임계값을 넘음 | `loading → timeout` | timeout 안내, Retry, Back | 해당 intent 취소; 후속 Action/query는 이전 성공 snapshot만 관찰 | URL body/response 미보관 | 15초 timeout 후 `StopLoading` 요청 및 stale completion 무시 |
| FR-BROWSER-009 | P0 | 새 탐색이 진행 중인 탐색을 대체 | `loading(A) → loading(B) → page/error(B)` | 최신 intent 결과만 표시 | monotonic intent ID와 `CancellationToken`; providers share latest state | 취소된 요청 metadata 미게시 | A 완료가 B 이후 도착해도 title/URL/View/A2UI가 A로 되돌아가지 않음 |
| FR-BROWSER-010 | P0 | 이전/다음 WebView history가 존재하거나 없음 | `page ↔ page`, no-op when unavailable | Back/Forward 활성·비활성 상태 | local WebView command; 성공 페이지 snapshot 갱신 | history 전체를 Action/annotation으로 노출하지 않음 | 비활성 제어는 focus order에서 제외; 활성 제어는 한 단계만 이동 |
| FR-BROWSER-011 | P0 | Back key 입력 | modal → tabs → page-history → app 순서 | 최상위 transient state 하나만 닫거나 이동 | View lifecycle이 숨은 surface를 즉시 제거 | Back이 private data를 노출하는 화면으로 이동하지 않음 | 한 번의 Back은 한 계층만 처리하고 취소 시 호출 제어로 포커스 복원 |
| FR-BROWSER-012 | P0 | Tabs 제어 활성화 | `page/home/error → tabs` | ordered tab cards, selected cue, New tab, Close | selected public page만 현재 Entity; tab card annotation은 구현 시 stable ID 사용 | normal metadata만 보임 | 최대 20개 탭, selected tab이 보이고 첫 포커스가 selected card |
| FR-BROWSER-013 | P0 | Tabs 화면에서 New tab, 현재 개수 <20 | `tabs → tabs(new selected)` | privacy-safe start tab | 새 stable ID; persistence snapshot에 포함 | 새 탭은 계정/추천/원격 thumbnail 없음 | 개수 +1, 새 탭 selected, 해당 card focus; 20개에서 disabled/설명 표시 |
| FR-BROWSER-014 | P0 | Tabs 화면에서 다른 tab 선택 | `tabs → page(selected)` | 선택 page의 주소/title/WebView | `GetCurrent`, View, A2UI를 선택 snapshot으로 갱신 | 비선택 tab body는 게시하지 않음 | 선택 후 Tabs 호출 제어 또는 주소로 포커스 복원, tab 순서 유지 |
| FR-BROWSER-015 | P0 | 2개 이상 탭에서 Close 선택 | `tabs → close-confirmation` | tab title을 포함한 Cancel/Close dialog | modal 동안 숨은 page View를 current visible로 주장하지 않음 | title은 80자로 truncate, URL/thumbnail은 dialog에 불필요하게 표시하지 않음 | modal 밖 focus 불가, 초기 포커스 Cancel, Back=Cancel |
| FR-BROWSER-016 | P0 | close-confirmation에서 Close | `close-confirmation → tabs/page` | tab 제거, nearest remaining tab selected | persistence/Entity/View 상태 원자 갱신 | 제거 tab metadata는 이후 persistence/annotation에 없음 | 한 탭만 제거되고 0개가 되지 않으며 호출 위치와 가장 가까운 tab focus |
| FR-BROWSER-017 | P0 | close-confirmation에서 Cancel/Back | `close-confirmation → tabs` | 변경 없음 | Entity/View/A2UI snapshot 불변 | 추가 기록 없음 | tab count/order/selected 불변, invoking Close 또는 tab card로 focus 복원 |
| FR-BROWSER-018 | P0 | 앱 pause/terminate/relaunch | active → persisted/restored | versioned normal session 또는 safe home fallback | 최대 20개 public `BrowserPage`; unsupported version은 fail-closed | cookie, credential, form, body, private mode 미저장 | valid snapshot은 order/selected/IDs 유지; malformed/unknown version은 crash 없이 home |
| FR-BROWSER-019 | P0 | Agent가 `GetCurrent` 호출 | page가 있거나 없음 | generated Browser Entity 또는 typed `not_found` | `Tizen.Entity.Browser.ToJson()`과 동일 field semantics | 공개 title/URL/bounded details만 반환 | success 1건과 no-current failure를 target RPC로 확인 |
| FR-BROWSER-020 | P0 | Agent가 `Go` 호출 | valid/invalid Browser Entity | queued success 또는 `invalid_input`/`unavailable` | UI와 동일 navigation path, self-RPC 없음 | scheme은 HTTP/HTTPS만, 길이 제한 | success 뒤 `GetCurrent`/`GetBrowserByIds` postcondition; invalid scheme bounded failure |
| FR-BROWSER-021 | P0 | Agent가 `GetBrowserByIds` 호출 | 1~50 IDs | 요청 순서·중복 보존, unresolved 분리 | generated resolver contract 준수 | ID ≤256, 빈/초과 batch 거부 | `[A, missing, A] → [A, A] + [missing]` target/host 증거 |
| FR-BROWSER-022 | P1 | Agent가 `ToCalendar` 호출 | page details가 calendar candidate를 제공하거나 아님 | typed conversion 또는 `unavailable`/`invalid_input` | generated ABI 그대로 유지 | 본문 scraping으로 event를 추측하지 않음 | 이번 scope에서 unsupported는 명시적 `unavailable`; output graph는 초기화됨 |
| FR-BROWSER-023 | P0 | 현재 page가 렌더되고 focus/resize/lifecycle 변동 | visible/focused annotation 갱신 | stable View ID, actual finite bounds, current focus | `GetAnnotatedViews`, `FindById`, `GetFocusedView`; generated Entity `ToJson()` | visible normal page metadata만 포함 | target에서 bounds 양수/finite, FindById 일치, 숨김/종료 시 stale View 없음 |
| FR-BROWSER-024 | P0 | `ToPresentation` 또는 `View_ToPresentation` | current visible state → Presentation | supported A2UI payload 또는 typed failure | canonical v0.9.1 + negotiated catalog; legacy v0.8는 명명된 호환 경로로만 유지 | arbitrary style/script/remote asset/body 금지 | 두 경로가 동일 entity/state를 표현하고 DisplayPresentation target render가 일치 |
| FR-BROWSER-025 | P0 | D-pad/keyboard/pointer/touch로 visible control 조작 | 동일 reducer command | 동일한 상태 전이와 피드백 | focus snapshot은 실제 NUI focus에서 파생 | 좌표/입력 로그에 private text 미포함 | 모든 visible enabled control은 각 입력 경로로 활성화 가능하고 두 가지 focus cue 제공 |
| FR-BROWSER-026 | P0 | modal/error/loading 중 Action/View query | transient state 관찰 | stale data를 반환하지 않는 bounded result | A2UI에는 현재 loading/error/action availability가 반영됨 | raw error/page content 미포함 | 상태 전환마다 Entity/View/A2UI 일관성 host test 및 target round trip 증거 |

## 범위 밖 처리

- Secret mode는 Samsung Browser의 중요한 privacy pattern으로 참조하지만 이번 제품에서 제공한다고 표시하지 않는다.
- 원격 thumbnail, 계정 avatar, proprietary icon/brand, AI 기능, download manager, extension, site permission 승인 UI는 구현하지 않는다.
- 인증/인증서/미디어 권한 요청은 자동 승인하지 않는다. 대상 WebView가 요구하면 명시적인 unavailable/deny 정책으로 처리하고 비밀 입력을 캡처하지 않는다.
