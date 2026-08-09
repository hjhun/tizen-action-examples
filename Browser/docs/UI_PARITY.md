# Browser UI parity ledger

갱신일: 2026-08-09 (Samsung visual refinement, refs-first)

- canonical preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)
- Samsung Android 근거: [`SAMSUNG_ANDROID_UI_REFERENCE.md`](SAMSUNG_ANDROID_UI_REFERENCE.md)
- 제품 요구사항: [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md)
- 현재 판정: **revised HTML와 Common Emulator 1920×1080 native visual parity PASS, semantic tree/RPC/A2UI/offline gate 차단**
- 증거 경계: `html-*`는 HTML-only 증거다. `native-browser-*-visual-*`는 최종 visual-refinement TPK의 Common Emulator/Aurum 증거이며 TV 제품 승인, typed Action/View RPC, semantic accessibility tree 또는 A2UI round trip을 증명하지 않는다. 세부 gate는 [`STAGE3_VALIDATION.md`](STAGE3_VALIDATION.md)에 있다.

## Visual refinement current HTML evidence

| 상태 | viewport | 증거 | 검증 결과 |
|---|---:|---|---|
| launch/home + address initial focus | 1920×1080 | [`images/html-browser-home-1920x1080.png`](images/html-browser-home-1920x1080.png) | PNG/RGB, 1920×1080, split address/navigation hierarchy와 initial focus cue 확인 |
| loading + disabled Reload + progress | 1280×720 | [`images/html-browser-loading-1280x720.png`](images/html-browser-loading-1280x720.png) | PNG/RGB, 1280×720, centered uniform scale 확인 |
| current page/search result | 1920×1080 | [`images/html-browser-page-1920x1080.png`](images/html-browser-page-1920x1080.png) | PNG/RGB, 1920×1080, public title/URL context와 content region 확인 |
| offline + Retry initial focus | 1280×720 | [`images/html-browser-offline-1280x720.png`](images/html-browser-offline-1280x720.png) | PNG/RGB, 1280×720, Retry/Back/Edit address 확인 |
| tabs cards/select/new/close | 1920×1080 | [`images/html-browser-tabs-1920x1080.png`](images/html-browser-tabs-1920x1080.png) | PNG/RGB, 1920×1080, full-canvas Tabs, local preview+title+URL, selected rail+focused outline 확인 |
| close confirmation + modal trap | 1920×1080 | [`images/html-browser-close-confirmation-1920x1080.png`](images/html-browser-close-confirmation-1920x1080.png) | PNG/RGB, split Cancel/red Close action, Cancel initial focus 확인 |
| close confirmation scaling | 1440×1080 | [`images/html-browser-close-confirmation-1440x1080.png`](images/html-browser-close-confirmation-1440x1080.png) | 0.75 centered uniform scale와 135px top/bottom letterbox 확인 |

기존 `html-browser-command-band-1280x720.png`, `html-browser-home-1280x720.png`, `html-browser-offline-1264x625.png`는 이전 sample의 역사적 HTML 증거이며 current parity 판정에는 사용하지 않는다.

## Visual refinement native evidence

| 상태 | 증거 | HTML↔NUI 판정 | Samsung reference 차이 / 의도한 Tizen 적용 |
|---|---|---|---|
| Home | [`images/native-browser-home-visual-1920x1080.png`](images/native-browser-home-visual-1920x1080.png) | PASS — 118-unit address header, expanded content, centered navigation dock, copy/controls unclipped | Samsung quick-access/account content는 fixture·command가 없어 만들지 않고 두 real start command만 유지 |
| real HTTPS Page | [`images/native-browser-page-visual-1920x1080.png`](images/native-browser-page-visual-1920x1080.png) | PASS — WebView가 최대 surface이며 URL/status 중복 row 없음 | phone bottom toolbar를 그대로 늘리지 않고 구현된 Back/Forward/Tabs만 TV-distance dock에 배치 |
| Tabs | [`images/native-browser-tabs-visual-1920x1080.png`](images/native-browser-tabs-visual-1920x1080.png) | PASS — full canvas, bounded preview/title/URL cards, selected rail와 별도 focus outline, circular close | local initial tile로 remote thumbnail을 대체; Search/More/Close all은 command 부재로 생략 |
| close confirmation | [`images/native-browser-close-confirmation-visual-1920x1080.png`](images/native-browser-close-confirmation-visual-1920x1080.png) | PASS — dimmed rounded surface, split Cancel/red Close, unclipped copy | Samsung close-all component family를 individual-close remote safety flow에 적용 |

모든 PNG는 Aurum native screenshot RPC에서 나온 1920×1080 RGB, non-blank frame이다. remote로 Tabs 진입, modal trap, Back 복원, 재진입, exact-one close(3→2)를 확인했다. coordinate click과 touch tap은 New tab 1→2→3의 화면 postcondition을 만들었다. system Back/Home overlay는 오른쪽 하단에 남지만 centered dock와 겹치지 않는다.

## Samsung reference ↔ revised HTML 시각 감사

| 화면 | 공식 Samsung Internet 관찰 | revised HTML 판정 | 차이 / 의도한 Tizen 적용 |
|---|---|---|---|
| Home/Page navigation | address/Reload와 하단 navigation 역할이 분리되고 web content가 주 surface | PASS — Back/Forward/Reload/Tabs를 한 top row에서 분리했고 content area를 확장함 | 공식 Home/Menu/Bookmarks/AI는 현재 real command가 없어 표시하지 않음. bottom dock은 1920×1080 D-pad hit target을 위해 icon+text 사용 |
| Home | address/search를 중심으로 새 탐색을 시작 | PASS — generic privacy card를 제거하고 단일 start message와 두 real command만 유지 | quick-access/remote thumbnail은 domain/fixture가 없고 privacy 경계 때문에 만들지 않음 |
| Tabs | 독립 screen, preview+title+URL, selected blue outline, circular X, New tab | PASS — full-canvas surface와 제한 폭 vertical card family가 source hierarchy를 보존 | preview는 remote screenshot 대신 local initial tile; Search/More/Close all은 command가 없어 생략 |
| Close dialog | dimmed rounded dialog, Cancel과 red Close 분할 action | PASS — centered TV dialog에 같은 action family와 red destructive label 적용 | 공식 화면은 close-all, 구현은 remote 오작동 방지용 individual-close 확인이라는 의도적 차이 |
| Focus/input | Android touch를 Tizen remote/keyboard/pointer/touch로 확장 | PASS(HTML) — outline+surface/scale, disabled skip, top/content/dock path, modal trap/restore | native focus geometry와 system overlay 충돌은 revised package 설치 후 판정 |

revised HTML을 3관점으로 재검토한 결과, Home/Page는 더 이상 모든 browser control이 경쟁하는 generic desktop top chrome으로 보이지 않고, Tabs는 generic full-width text row 대신 Samsung의 preview+metadata+close component family를 알아볼 수 있다. NUI에서 이 결론을 재사용하려면 native screenshot이 같은 hierarchy와 density를 보존해야 하며, HTML 판정을 native PASS로 확대하지 않는다.

## Samsung reference → HTML → NUI mapping

| UI slice | Samsung reference/translation | Stage 1 HTML contract | production NUI/runtime mapping | native evidence/status |
|---|---|---|---|---|
| 1920×1080 canvas | phone 화면을 확대하지 않고 TV-distance hierarchy로 번역 | viewport 안에서 centered uniform transform; 4-shape geometry PASS | full-window physical root + one centered NUI ancestor transform, `WindowSize`/`GetInsets()` resize/inset 갱신 | Common 1920×1080 PASS; non-zero inset native 미검증 |
| page-first hierarchy | Samsung Browser address/action + bottom navigation 역할 분리 | 118-unit address header, 6-unit progress, 확장 content, bottom dock | 동일 split hierarchy와 real content-only `WebView` sibling | revised Common native Home/Page PASS |
| Back/Forward/Reload | Samsung 공식 navigation controls | Back/Forward는 bottom dock, Reload는 address 인접; history/loading disabled | real WebView Reload/GoBack/GoForward/CanGo*, loading disabled-skip focus 유지 | disabled native cue와 Page dock PASS; actual multi-entry history는 미검증 |
| address/search | 하나의 address/search surface | URL/search normalization, Enter, local search fixture, invalid input recovery | bounded URL 또는 fixed HTTPS search, credential rejection, public query/fragment redaction | source/host PASS; real target search 미검증 |
| loading | chrome/context를 잃지 않는 recoverable navigation | progress, loading mask, latest intent state | synchronous Loading state, visible progress, active request cancellation/StopLoading | native LOADING/progress frame PASS; ≤100/500ms timing 미측정 |
| page | web content가 가장 큰 surface | privacy-safe local article은 WebView region의 실행형 대체물 | real system WebView만 제품 gate 충족 | public HTTPS success PASS |
| offline/error/timeout | 실패 설명과 직접 recovery | Offline, Engine error, Timeout, Retry/Back/Edit address | typed bounded state, NUI recovery surface, focus trap, stable-page/home Back, 15초 timeout | InvalidInput/Retry native PASS; offline capture/engine/timeout 미검증 |
| tabs manager | 별도 Tabs screen, selected outline, per-tab X, New tab | full-canvas preview+title+URL cards, 1~20 select/new/close, max disabled, scroll focus | bounded workspace와 같은 card hierarchy | revised 3-card native PASS; max-20 native scroll는 미검증 |
| close dialog | Samsung close-all dialog family | individual close safety adaptation, split Cancel/red Close, trap, Back cancel, restore | 동일 NUI modal action family와 geometry | revised native trap/Back restore/exact-one close PASS |
| input | touch Android를 D-pad/keyboard/pointer/touch로 확장 | Arrow/Enter/Escape, click, touch tap가 같은 command를 실행 | one reducer, `FocusManager`, TouchEvent | remote/pointer/tap state postcondition PASS; semantic tree는 root 0 |
| privacy | Secret mode/privacy priority는 참조하되 범위 밖 | normal-only 설명, remote asset/request 없음, public fixture만 사용 | body/cookie/form/credential 미게시 | projection target 검증 미실행 |
| Entity/View/A2UI | 현재 visible page context만 Agent에 제공 | HTML은 semantic state만 preview | transient/secondary/lifecycle suppression, generated Entity `ToJson()`, actual bounds, official canonical producer + named legacy adapter | source/schema/parser PASS; installed RPC/render 미검증 |

## Browser verification matrix

| 항목 | 결과 |
|---|---|
| direct `file:` open / build step 없음 | PASS |
| 외부 network request | PASS — document `file:` request 1개만 관찰 |
| console error / page error | PASS — 각각 0 |
| primary flow | PASS — home → search submit → loading → page |
| exceptional flow | PASS — offline, engine-error, timeout, invalid-input |
| D-pad/keyboard | PASS — header/body, Tabs, scrolled 10th tab, Enter, Back/Escape |
| pointer/touch | PASS — Tabs/New tab activation |
| modal | PASS — Cancel initial focus, Tab/Shift+Tab trap, Back cancel, invoking Close focus restore, confirm close 1개 제거 |
| disabled/bounds | PASS — Back/Forward/Reload state, 20 tabs, New tab disabled |
| responsive geometry | PASS — 1920×1080, 1280×720, 1440×1080, 2560×1080 centered uniform transform |
| screenshot | PASS — PNG 6개 decode, dimensions, non-blank, privacy-safe visual inspection |

## Installed native evidence and remaining differences

현재 visual-refinement package의 Common Emulator 증거는 revised Home, real HTTPS Page, Tabs, close confirmation 4개다. 이전 Stage 3의 Loading/InvalidInput은 reducer/WebView 구조가 유지된 역사적 runtime 증거지만 current visual parity PNG로 대체 주장하지 않는다. 기존 command-band/address-focus/tabs-focus도 역사적 baseline으로만 유지한다.

Stage 2A/2B/2C/2D에서 source/host 기준으로 닫은 차이:

1. NUI geometry를 당시 HTML 계약에 맞췄고, visual refinement에서 다시 118/0/6/924 split address/content 계약으로 갱신했다.
2. drawable area를 검증한 뒤 physical root 위의 단일 1920×1080 ancestor만 uniform scale/center하도록 분리했다.
3. disabled Back/Forward를 건너뛰고 command row ↔ content WebView를 결정적으로 이동하는 포커스 그래프를 추가했다.
4. URL/search 입력, Loading/Page/Offline/EngineError/Timeout/InvalidInput, Retry/Back/Edit address를 한 navigation state path로 연결했다.
5. 실제 WebView Reload/Back/Forward와 history availability, 15초 timeout, superseded `StopLoading`을 adapter에 연결했다.
6. 1~20 normal tabs, privacy-safe Home, selected/close cues, 개별 close confirmation, v2 atomic persistence를 NUI/use-case/domain에 연결했다.
7. visible normal Page만 Entity/View로 게시하고 loading/error/Tabs/modal/pause/terminate 및 tab-selection 전환에서 이전 snapshot을 제거한다.
8. current generated Entity `ToJson()` annotation과 measured finite bounds/focus registry를 연결하고 forged/non-current Presentation input을 거부한다.
9. official v0.9.1 canonical stream과 current DisplayPresentation legacy parser adapter를 한 bounded snapshot에서 분리했다.

Stage 3에서 닫은 차이:

1. Home의 비어 있던 native surface를 실제 WebView/주소 명령과 normal-mode privacy 설명이 있는 content-first hierarchy로 교정했다.
2. header brand, context/status, Home 설명, privacy card, close modal title/body의 native clipping을 제거했다.
3. 실제 HTTPS Page, Loading, InvalidInput/Retry, ordered Tabs, modal trap/Back restore/exact-one close를 1920×1080 native frame으로 확인했다.

Visual refinement에서 추가로 닫은 차이:

1. 모든 navigation command가 경쟁하던 top row를 address/Reload header와 Back/Forward/Tabs dock으로 분리하고 중복 URL/status band를 제거했다.
2. Tabs를 full-canvas preview/title/URL card surface로 교체하고 selected rail, focus outline, circular close를 분리했다.
3. close dialog를 Samsung 계열 split action surface로 교체했고 Home, dock, count, modal의 target clipping을 제거했다.
4. persisted blank title target crash를 `New tab` fallback + 80자 bound 계약과 host regression test로 닫았다.

남은 차이:

1. non-zero inset, multi-resolution native, max-20 scroll, real history Back/Forward, engine error/timeout, pause/resume 장면은 host/source만 검증됐다.
2. Aurum tree는 root 0이므로 accessibility semantic tree는 검증하지 못했다. 다만 이번 `tap`은 New tab count/card가 2→3으로 변한 화면 postcondition까지 확인했다.
3. offline 전환은 SDB/Aurum transport도 끊어 native offline frame을 캡처하지 못했다.
4. installed Action/View RPC와 legacy DisplayPresentation round trip은 generated/runtime ABI mismatch로 차단됐다. canonical v0.9.1 target render는 negotiated ordered-message transport 부재로 별도 차단 상태다.

## Runtime blocker boundary

기존 Common Emulator에서는 generated provider dispatch가 `StubBase.HasPrivilegeLocal`의 `MissingMethodException`으로 앱을 종료했다. 따라서 provider discovery를 typed Action/View RPC, measured ViewAnnotation, A2UI round trip 성공으로 확장 해석하지 않는다. Stage 3에서 sanctioned `actionc`/Tizen runtime compatibility path만 재진단하며 generated source나 platform schema는 수정하지 않는다.

## English evidence summary

The revised HTML and final installed NUI share the same source-backed hierarchy: compact address/Reload chrome, page-first content, a separate navigation dock, full-canvas tab cards, and a split-action close dialog. The final 1920×1080 native frames are visually aligned with that prototype, including non-color selected/focus cues and unclipped TV-distance labels. Remote, pointer, and touch inputs produced observable state changes; closing one tab changed the count from three to two.

This parity result is limited to the Browser visual module on the Common Emulator. The Aurum accessibility tree is empty, non-zero-inset and multi-resolution native modes remain unverified, and the existing generated RPC, dependent ViewAnnotation/legacy Display, canonical A2UI transport, and offline-capture blockers remain unchanged.
