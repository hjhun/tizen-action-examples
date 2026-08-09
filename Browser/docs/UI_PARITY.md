# Browser UI parity ledger

갱신일: 2026-08-09 (Stage 2C)

- canonical preview: [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html)
- Samsung Android 근거: [`SAMSUNG_ANDROID_UI_REFERENCE.md`](SAMSUNG_ANDROID_UI_REFERENCE.md)
- 제품 요구사항: [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md)
- 현재 판정: **Stage 1 HTML 계약 PASS, installed NUI parity 미충족**
- 증거 경계: 아래 `html-*` 파일은 Playwright Chromium으로 검증한 HTML-only 증거다. Common Emulator, real WebView, Action/View RPC, A2UI 또는 native input을 증명하지 않는다.

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
| 1920×1080 canvas | phone 화면을 확대하지 않고 TV-distance hierarchy로 번역 | viewport 안에서 centered uniform transform; 4-shape geometry PASS | full-window physical root + one centered NUI ancestor transform, `WindowSize`/`GetInsets()` resize/inset 갱신 | host geometry PASS; non-zero inset native 미검증 |
| page-first hierarchy | Samsung Browser page + navigation toolbar | 132-unit command band, 92-unit context, 나머지는 content region | 동일 132/92/6 geometry와 1816×806 real content-only `WebView` sibling | source/host PASS; 설치 화면 미검증 |
| Back/Forward/Reload | Samsung 공식 navigation controls | history availability에 따라 Back/Forward disabled, loading/tabs에서 Reload disabled | real WebView Reload/GoBack/GoForward/CanGo*, loading disabled-skip focus | source/host PASS; actual native history 미검증 |
| address/search | 하나의 address/search surface | URL/search normalization, Enter, local search fixture, invalid input recovery | bounded URL 또는 fixed HTTPS search, credential rejection, public query/fragment redaction | source/host PASS; real target search 미검증 |
| loading | chrome/context를 잃지 않는 recoverable navigation | progress, loading mask, latest intent state | synchronous Loading state, visible progress, active request cancellation/StopLoading | source/host PASS; native timing/frame 미검증 |
| page | web content가 가장 큰 surface | privacy-safe local article은 WebView region의 실행형 대체물 | real system WebView만 제품 gate 충족 | real HTTPS success 미검증 |
| offline/error/timeout | 실패 설명과 직접 recovery | Offline, Engine error, Timeout, Retry/Back/Edit address | typed bounded state, NUI recovery surface, focus trap, stable-page/home Back, 15초 timeout | source/host PASS; native error/timeout 미검증 |
| tabs manager | 별도 Tabs screen, selected outline, per-tab X, New tab | ordered 1~20 rows, select/new/close, max disabled, scroll focus | bounded workspace, clipped/scrolling NUI rows, selected cue, New tab, stable IDs, session v2 | source/host PASS; native rows/scroll 미검증 |
| close dialog | Samsung close-all dialog family | individual close safety adaptation, Cancel initial focus, trap, Back cancel, restore | full-canvas NUI modal, 80-char title, Cancel↔Close trap, invoking close/nearest focus | source/host PASS; native modal 미검증 |
| input | touch Android를 D-pad/keyboard/pointer/touch로 확장 | Arrow/Enter/Escape, click, touch tap가 같은 command를 실행 | one reducer, `FocusManager`, TouchEvent | command-band remote 일부만 과거 검증 |
| privacy | Secret mode/privacy priority는 참조하되 범위 밖 | normal-only 설명, remote asset/request 없음, public fixture만 사용 | body/cookie/form/credential 미게시 | projection target 검증 미실행 |
| Entity/View/A2UI | 현재 visible page context만 Agent에 제공 | HTML은 semantic state만 preview | generated Browser Entity, actual bounds, canonical v0.9.1 + legacy adapter | generated runtime blocker로 target 미검증 |

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

## Installed native baseline and remaining differences

과거 Common Emulator capture는 [`images/native-browser-command-band-1920x1080.png`](images/native-browser-command-band-1920x1080.png), [`images/native-browser-address-focus-1920x1080.png`](images/native-browser-address-focus-1920x1080.png), [`images/native-browser-tabs-focus-1920x1080.png`](images/native-browser-tabs-focus-1920x1080.png)이다. 이들은 zero-inset command band와 address→Tabs remote focus 변화만 증명한다.

Stage 2A/2B/2C에서 source/host 기준으로 닫은 차이:

1. NUI header/context/progress/content geometry를 HTML의 132/92/6/806 계약과 일치시켰다.
2. drawable area를 검증한 뒤 physical root 위의 단일 1920×1080 ancestor만 uniform scale/center하도록 분리했다.
3. disabled Back/Forward를 건너뛰고 command row ↔ content WebView를 결정적으로 이동하는 포커스 그래프를 추가했다.
4. URL/search 입력, Loading/Page/Offline/EngineError/Timeout/InvalidInput, Retry/Back/Edit address를 한 navigation state path로 연결했다.
5. 실제 WebView Reload/Back/Forward와 history availability, 15초 timeout, superseded `StopLoading`을 adapter에 연결했다.
6. 1~20 normal tabs, privacy-safe Home, selected/close cues, 개별 close confirmation, v2 atomic persistence를 NUI/use-case/domain에 연결했다.

남은 차이:

1. typography/token의 installed-state parity와 navigation/error/history/tabs/modal pointer·remote, non-zero inset, lifecycle native evidence가 없다.
2. current-state canonical A2UI와 두 DisplayPresentation round trip이 없다.

## Runtime blocker boundary

기존 Common Emulator에서는 generated provider dispatch가 `StubBase.HasPrivilegeLocal`의 `MissingMethodException`으로 앱을 종료했다. 따라서 provider discovery를 typed Action/View RPC, measured ViewAnnotation, A2UI round trip 성공으로 확장 해석하지 않는다. Stage 3에서 sanctioned `actionc`/Tizen runtime compatibility path만 재진단하며 generated source나 platform schema는 수정하지 않는다.
