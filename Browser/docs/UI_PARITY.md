# Browser UI parity ledger

갱신일: 2026-08-09 (Stage 3 final-partial)

- canonical preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)
- Samsung Android 근거: [`SAMSUNG_ANDROID_UI_REFERENCE.md`](SAMSUNG_ANDROID_UI_REFERENCE.md)
- 제품 요구사항: [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md)
- 현재 판정: **Stage 1 HTML 계약 PASS, Common Emulator native product flow 부분 PASS, RPC/A2UI/offline target gate 차단**
- 증거 경계: `html-*`는 HTML-only 증거다. `native-browser-*-stage3-*`는 Common Emulator/Aurum 증거이며 TV 제품 승인, typed Action/View RPC 또는 A2UI round trip을 증명하지 않는다. 세부 gate는 [`STAGE3_VALIDATION.md`](STAGE3_VALIDATION.md)에 있다.

## Stage 1 current HTML evidence

| 상태 | viewport | 증거 | 검증 결과 |
|---|---:|---|---|
| launch/home + address initial focus | 1920×1080 | [`images/html-browser-home-1920x1080.png`](images/html-browser-home-1920x1080.png) | PNG/RGB, 1920×1080, local fixture, initial focus cue 확인 |
| loading + disabled Reload + progress | 1280×720 | [`images/html-browser-loading-1280x720.png`](images/html-browser-loading-1280x720.png) | PNG/RGB, 1280×720, centered uniform scale 확인 |
| current page/search result | 1920×1080 | [`images/html-browser-page-1920x1080.png`](images/html-browser-page-1920x1080.png) | PNG/RGB, 1920×1080, public title/URL context와 content region 확인 |
| offline + Retry initial focus | 1280×720 | [`images/html-browser-offline-1280x720.png`](images/html-browser-offline-1280x720.png) | PNG/RGB, 1280×720, Retry/Back/Edit address 확인 |
| tabs list/select/new/close | 1920×1080 | [`images/html-browser-tabs-1920x1080.png`](images/html-browser-tabs-1920x1080.png) | PNG/RGB, 1920×1080, selected+focused two-cue와 ordered rows 확인 |
| close confirmation + modal trap | 1440×1080 | [`images/html-browser-close-confirmation-1440x1080.png`](images/html-browser-close-confirmation-1440x1080.png) | PNG/RGB, 1440×1080, 0.75 scale/135px letterbox와 Cancel focus 확인 |

기존 `html-browser-command-band-1280x720.png`, `html-browser-home-1280x720.png`, `html-browser-offline-1264x625.png`는 이전 sample의 역사적 HTML 증거이며 current parity 판정에는 사용하지 않는다.

## Samsung reference → HTML → NUI mapping

| UI slice | Samsung reference/translation | Stage 1 HTML contract | production NUI/runtime mapping | native evidence/status |
|---|---|---|---|---|
| 1920×1080 canvas | phone 화면을 확대하지 않고 TV-distance hierarchy로 번역 | viewport 안에서 centered uniform transform; 4-shape geometry PASS | full-window physical root + one centered NUI ancestor transform, `WindowSize`/`GetInsets()` resize/inset 갱신 | Common 1920×1080 PASS; non-zero inset native 미검증 |
| page-first hierarchy | Samsung Browser page + navigation toolbar | 132-unit command band, 92-unit context, 나머지는 content region | 동일 132/92/6 geometry와 1816×806 real content-only `WebView` sibling | installed frame/real WebView PASS |
| Back/Forward/Reload | Samsung 공식 navigation controls | history availability에 따라 Back/Forward disabled, loading/tabs에서 Reload disabled | real WebView Reload/GoBack/GoForward/CanGo*, loading disabled-skip focus | source/host PASS; actual native history 미검증 |
| address/search | 하나의 address/search surface | URL/search normalization, Enter, local search fixture, invalid input recovery | bounded URL 또는 fixed HTTPS search, credential rejection, public query/fragment redaction | source/host PASS; real target search 미검증 |
| loading | chrome/context를 잃지 않는 recoverable navigation | progress, loading mask, latest intent state | synchronous Loading state, visible progress, active request cancellation/StopLoading | native LOADING/progress frame PASS; ≤100/500ms timing 미측정 |
| page | web content가 가장 큰 surface | privacy-safe local article은 WebView region의 실행형 대체물 | real system WebView만 제품 gate 충족 | public HTTPS success PASS |
| offline/error/timeout | 실패 설명과 직접 recovery | Offline, Engine error, Timeout, Retry/Back/Edit address | typed bounded state, NUI recovery surface, focus trap, stable-page/home Back, 15초 timeout | InvalidInput/Retry native PASS; offline capture/engine/timeout 미검증 |
| tabs manager | 별도 Tabs screen, selected outline, per-tab X, New tab | ordered 1~20 rows, select/new/close, max disabled, scroll focus | bounded workspace, clipped/scrolling NUI rows, selected cue, New tab, stable IDs, session v2 | native 3-tab rows/select/new/close PASS; max-20 native 미검증 |
| close dialog | Samsung close-all dialog family | individual close safety adaptation, Cancel initial focus, trap, Back cancel, restore | full-canvas NUI modal, 80-char/fallback title, Cancel↔Close trap, invoking close/nearest focus | native trap/cancel/restore/3→2 confirm PASS |
| input | touch Android를 D-pad/keyboard/pointer/touch로 확장 | Arrow/Enter/Escape, click, touch tap가 같은 command를 실행 | one reducer, `FocusManager`, TouchEvent | remote/OSK/pointer click PASS; tap semantic activation 부분 |
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

## Installed native Stage 3 evidence and remaining differences

현재 Common Emulator 증거는 Home, Loading, real HTTPS Page, InvalidInput, Tabs, close confirmation 6개이며 [`STAGE3_VALIDATION.md`](STAGE3_VALIDATION.md)에 출처와 gate를 기록했다. 기존 command-band/address-focus/tabs-focus 3개는 역사적 baseline으로만 유지한다.

Stage 2A/2B/2C/2D에서 source/host 기준으로 닫은 차이:

1. NUI header/context/progress/content geometry를 HTML의 132/92/6/806 계약과 일치시켰다.
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

남은 차이:

1. non-zero inset, multi-resolution native, max-20 scroll, real history Back/Forward, engine error/timeout, pause/resume 장면은 host/source만 검증됐다.
2. Aurum tree는 root 0이므로 accessibility semantic tree는 검증하지 못했다. `tap` status 0도 semantic touch activation 증거로 확대하지 않는다.
3. offline 전환은 SDB/Aurum transport도 끊어 native offline frame을 캡처하지 못했다.
4. installed Action/View RPC와 legacy DisplayPresentation round trip은 generated/runtime ABI mismatch로 차단됐다. canonical v0.9.1 target render는 negotiated ordered-message transport 부재로 별도 차단 상태다.

## Runtime blocker boundary

기존 Common Emulator에서는 generated provider dispatch가 `StubBase.HasPrivilegeLocal`의 `MissingMethodException`으로 앱을 종료했다. 따라서 provider discovery를 typed Action/View RPC, measured ViewAnnotation, A2UI round trip 성공으로 확장 해석하지 않는다. Stage 3에서 sanctioned `actionc`/Tizen runtime compatibility path만 재진단하며 generated source나 platform schema는 수정하지 않는다.
