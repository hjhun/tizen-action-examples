# Browser Samsung Internet 시각 정제 기록

검토일: 2026-08-09
기준 커밋: `12d960858282bae70310322328c59fa158652f25`
대상: `org.tizen.browser`의 HTML/NUI 시각 구조만 해당

## 1. 재검토한 권위 자료

| ID | 출처 | 버전·접근일 | 직접 확인한 내용 | 확인하지 않은 내용 |
|---|---|---|---|---|
| VIS-SAM-001 | [Samsung Browser — Google Play](https://play.google.com/store/apps/details?id=com.sec.android.app.sbrowser) | `30.0.0.67`, 2026-07-08 갱신, 2026-08-09 접근 | Samsung Electronics 배포, 현재 공개 버전과 privacy posture | 지원 문서 screenshot이 정확히 같은 build인지 여부 |
| VIS-SAM-002 | [Use the navigation bar in Samsung Browser](https://www.samsung.com/us/support/answer/ANS10012955/) | Galaxy S26 지원 화면, 2026-08-09 접근 | 상단 address/search와 Reload, 하단 Back/Forward/Home/Tabs/Menu 계열, 웹 콘텐츠 우선 구조 | One UI 세부 버전과 px geometry |
| VIS-SAM-003 | [Open or close tabs in Samsung Browser](https://www.samsung.com/us/support/answer/ANS10012961/) | Galaxy S26 지원 화면, 2026-08-09 접근 | 별도 `Tabs` 화면, list/grid/stack 선택, preview+title+URL 카드, selected blue outline, per-tab `X`, `New tab`, `More options`, `Close all tabs` 확인 | 개별 tab close가 확인 dialog를 요구하는지 여부 |
| VIS-SAM-004 | 같은 Tabs 공식 자료의 `Close all tabs` 화면 | 2026-08-09 접근 | dimmed 배경, 큰 rounded dialog, `Cancel`/red `Close` 분할 action, destructive 확인 | 개별 close 문구와 TV focus 동작 |
| VIS-SAM-005 | [One UI Design Guidelines](https://design.samsung.com/global/contents/one-ui/download/oneui_design_guide_eng.pdf) | 2026-08-09 접근 | browse surface의 action hierarchy, bottom toolbar와 navigation의 역할 분리 | 현재 Samsung Browser 전용 시각 규격 |

공식 screenshot은 감사 중 임시로만 확인하며 저장소에 복사하지 않는다. One UI/app build가 출처에 명시되지 않은 부분은 추정하지 않는다.

## 2. 기존 HTML·native 화면의 3관점 감사

검사한 설치 화면은 `native-browser-home-stage3-1920x1080.png`, `native-browser-page-stage3-1920x1080.png`, `native-browser-tabs-stage3-1920x1080.png`, `native-browser-close-confirmation-stage3-1920x1080.png`이며 HTML의 동등 상태도 함께 비교했다.

### Architect — 정보 구조와 Android→TV 번역

- PASS: real WebView가 가장 큰 제품 surface이고 Tabs가 별도 상태이며, 주소/검색·history disabled·Reload·loading/error·modal command mapping이 실제 reducer/use case에 연결돼 있다.
- 차이: 모든 탐색 제어를 132-unit 상단 한 줄에 모아 Samsung Internet의 address 영역과 navigation toolbar 역할 분리가 사라졌다. 주소 아래 92-unit title/URL/status row도 URL을 중복하고 웹 콘텐츠를 줄인다.
- 차이: Tabs overlay가 기존 desktop chrome 아래에서 열리므로 독립적인 Samsung `Tabs` 화면이 아니라 웹 앱 안의 generic list처럼 보인다.
- 교정: page/home/loading/error는 상단 compact address+Reload와 하단 navigation dock으로 분리한다. Tabs는 canvas 전체를 소유하는 secondary surface로 만들고 Back 계층으로 복귀한다.

### Product / visual designer — hierarchy와 component family

- PASS: neutral surface, blue selected/focus, red destructive, 큰 WebView, restrained motion은 기준 방향에 맞는다.
- 차이: `Browser` brand, 세 개의 사각 history button, 긴 address, Tabs button이 한 줄에서 같은 무게를 가져 generic desktop top chrome으로 읽힌다.
- 차이: Home의 marketing hero+privacy card와 full-width Tabs row는 Samsung Browser보다 generic dashboard/list component에 가깝다. native Tabs는 title과 URL이 한 줄에 합쳐져 계층도 약하다.
- 차이: 기존 modal은 별도 boxed button 두 개를 둔 generic dialog다. 공식 Samsung dialog의 넓은 surface와 분할 text action, red destructive label 계열이 약하다.
- 교정: product identity는 작게, address를 주인공으로, Reload만 인접시킨다. Home은 card를 제거한 단일 start message로 정리한다. Tabs는 제한 폭의 preview+title+URL 카드와 circular close를 사용한다. modal은 title/body와 분할 action 영역을 갖는 Samsung 계열로 수정한다.

### CX / accessibility — D-pad와 overlay 충돌

- PASS: 초기 focus address, disabled skip, modal Cancel initial focus, Back cancel, invoking close 복원은 host/native 증거가 있다.
- 차이: 상단 한 행이 길어 TV에서 focus 이동 거리가 길고, page content로 내려간 뒤 자주 쓰는 history/Tabs로 되돌아가려면 상단까지 이동해야 한다.
- 차이: selected tab과 focused tab의 cue가 모두 blue border에 치우쳐 구분이 약하고, native tab row가 title/URL을 구조적으로 분리하지 않는다.
- 교정: top row는 Address↔Reload로 제한하고 bottom dock은 Back↔Forward↔Tabs로 제한한다. content를 가운데 전이 계층으로 두고 Up/Down 경로를 명시한다. selected cue는 leading blue rail/soft surface, focus cue는 굵은 outline+scale/surface로 분리한다. modal 밖 focus와 pointer activation은 차단하고 Back은 호출 close로 복원한다.

## 3. 구현 가능한 대안

### 대안 A — 기존 단일 top row 정제

Back/Forward/Reload/Address/Tabs를 계속 한 행에 두되 간격·shape·type만 Samsung token에 가깝게 조정한다.

- 장점: NUI 변경과 focus risk가 가장 작다.
- 단점: 모든 제어가 주소와 경쟁하는 desktop chrome이라는 근본 문제가 남는다. Samsung Internet phone navigation model의 역할 분리를 설명할 수 없다.
- 판정: 기각.

### 대안 B — 상단 address + 하단 navigation + 독립 Tabs surface

상단에는 작은 product context, address/search, Reload만 둔다. 하단에는 현재 실제 명령인 Back, Forward, Tabs만 둔다. Home/Page/Loading/Error는 같은 page-first content region을 공유하고 Tabs는 canvas 전체를 덮는 전용 관리 화면을 사용한다.

- 장점: 공식 Samsung Internet의 address/action/navigation 분리와 page-first mental model을 보존하면서 TV D-pad 경로를 짧게 만든다. 기능 없는 Menu/Bookmarks/AI를 만들지 않는다.
- 단점: focus graph와 NUI geometry를 함께 바꿔야 하며 bottom system overlay와 겹치지 않는 native 확인이 필요하다.
- 판정: 선택.

### 대안 C — 좌측 navigation rail + 우측 page canvas

Back/Forward/Tabs를 TV용 좌측 rail에 고정하고 address를 상단에 둔다.

- 장점: D-pad 방향성과 content 폭이 명확하다.
- 단점: 현재 Samsung Internet에서 근거를 찾을 수 없는 TV dashboard/navigation rail을 새로 발명한다. Android 정신 모델보다 저장소 전용 shell에 가까워진다.
- 판정: 기각.

## 4. 선택 설계와 수용 기준

### Page/Home/Loading/Error

- 1920×1080 reference canvas와 inset-aware 단일 ancestor transform은 유지한다.
- 상단 118-unit 영역은 small product identity, 70-unit address/search capsule, 인접 Reload로 구성한다.
- 기존 title/URL/status 중복 band를 제거하고 6-unit progress만 상단 chrome 아래에 둔다. 주소가 current URL을 소유하고 상태는 content surface의 loading/recovery copy로 전달한다.
- bottom dock은 safe area 안의 source-backed navigation toolbar adaptation이다. 실제 command가 있는 Back, Forward, Tabs만 노출한다. 구현되지 않은 Home/Menu/Bookmarks/Galaxy AI는 장식으로 만들지 않는다.
- Home은 hero card pair를 제거하고 한 개의 start message, `Open Tizen guide`, `Enter an address`, normal-only privacy line만 둔다.
- WebView는 가장 큰 영역을 유지하고 dock과 OS overlay가 핵심 content/focus를 가리지 않는지 native 1920×1080에서 확인한다.

### Tabs

- Tabs surface는 desktop chrome을 숨기고 canvas 전체를 소유한다.
- header는 Back, 큰 `Tabs` title, normal tab count만 제공한다. Search/More/Close all은 현재 command가 없으므로 표시하지 않는다.
- 각 tab은 local privacy-safe preview block, 분리된 title/URL, circular close를 가진 제한 폭 card다. selected는 leading blue rail+soft fill, focus는 5px outline+surface/scale로 구분한다.
- `New tab`은 하단 action으로 유지하고 20개에서 disabled 설명을 보존한다.

### Close dialog

- dimmed 전체 canvas 위 중앙 rounded surface를 사용한다.
- 개별 close는 Tizen remote 오작동 방지 adaptation임을 유지한다. title은 최대 80자, URL/thumbnail은 dialog에 넣지 않는다.
- `Cancel`과 red `Close`는 하나의 분할 action 영역을 이루며 초기 focus는 Cancel이다. Left/Right만 이동하고 Up/Down은 modal을 벗어나지 않으며 Back은 Cancel과 동일하게 호출 close에 복원한다.

### Focus와 입력

- top row: `Address ↔ Reload`.
- content 계층: Address/Reload에서 Down으로 Home action, recovery action 또는 WebView에 진입하고 Up으로 Address에 복귀한다.
- bottom dock: `Back ↔ Forward ↔ Tabs`; disabled item은 제외한다. content에서 Down으로 dock에 진입하고 dock에서 Up으로 content에 복귀한다.
- Tabs: header Back → ordered cards/open↔close → New tab. pointer/touch는 같은 command callback을 사용한다.
- focus는 outline과 surface/scale 두 cue를 유지한다. selected와 disabled는 색 외 shape/opacity/rail 차이를 갖는다.

### 검증 경계

- HTML에서 keyboard/D-pad, Enter/Back, pointer/touch, disabled state, modal trap/restoration, 1920×1080·1280×720·1440×1080·2560×1080 scaling을 먼저 검증한다.
- NUI는 Browser host executable tests와 clean solution build 후 Common Emulator에 update-install한다.
- Aurum root가 계속 0이면 semantic accessibility tree는 `BLOCKED/capability limit`로 유지하고 key/coordinate input+fresh screenshot만 증거로 사용한다.
- generated `HasPrivilegeLocal` direct call과 target RPCPort ABI가 맞지 않으면 generated binding을 수정하지 않고 framework generator/runtime blocker로 기록한다. 이전 post-generation compatibility experiment는 historical evidence이며 새 generation에 재적용하지 않는다. Browser/View Action E2E는 별도 검증하되 canonical A2UI transport blocker와 transport를 끊는 offline gate는 재수정·재실험하지 않고 차단 상태를 유지한다.

## 5. 구현·검증 결과

### refs-first 결과

- `refs/one-ui-sample.html`을 먼저 수정한 뒤 Playwright Chromium으로 1개의 통합 시나리오를 실행했다. Home→Loading→Page→Tabs→close modal→Back 복원→confirm close와 Offline을 거쳤고, console/page error는 각각 0, 외부 request는 0이었다.
- keyboard/D-pad, Enter/Escape, pointer click, touch `tap`, disabled Back/Forward/Reload, modal trap, 20-tab bound, 1920×1080·1280×720·1440×1080·2560×1080 centered transform을 확인했다.
- Home/Page는 상단 address+Reload와 하단 navigation으로 분리됐고, Tabs는 전체 canvas의 preview/title/URL card family로 바뀌었다. [`UI_PARITY.md`](UI_PARITY.md)의 revised HTML PNG를 시각 검사한 뒤 NUI 구현으로 진행했다.
- 한국어 prototype 보고와 HTML Home/Tabs 이미지는 Telegram 대상에 전송 성공했다.

### native 결과

| 상태 | Common Emulator 증거 | 판정 |
|---|---|---|
| Home | [`images/native-browser-home-visual-1920x1080.png`](images/native-browser-home-visual-1920x1080.png) | PASS — 단일 start hierarchy, unclipped copy, address initial focus, disabled history와 3-command dock |
| Page | [`images/native-browser-page-visual-1920x1080.png`](images/native-browser-page-visual-1920x1080.png) | PASS — 실제 public HTTPS WebView가 최대 surface, 중복 URL/status band 없음, dock가 system overlay와 분리됨 |
| Tabs | [`images/native-browser-tabs-visual-1920x1080.png`](images/native-browser-tabs-visual-1920x1080.png) | PASS — full-canvas header, 3개 ordered card, local preview/title/URL, selected rail, focus outline, circular close |
| Close confirmation | [`images/native-browser-close-confirmation-visual-1920x1080.png`](images/native-browser-close-confirmation-visual-1920x1080.png) | PASS — dimmed rounded surface, unclipped title/body, split Cancel/red Close, Cancel initial focus |

- 설치 직후 persisted blank page title이 `title[..1]`에 진입해 종료되는 target-only 경계를 발견했다. blank/whitespace title을 `New tab`으로 바꾸고 80자로 제한하는 `BrowserTabVisualText` 계약과 host test를 추가한 뒤 다시 package/install했다.
- native 비교 중 Home 설명, Forward label, tab count, modal 설명의 잘림을 수정하고 최종 package에서 네 화면을 모두 다시 캡처했다.
- remote Down/Enter로 Tabs에 진입했고, modal에서 Right/Down/Left가 경계를 벗어나지 않으며 Back이 invoking close로 복원됨을 확인했다. 재진입 후 Close는 tab count를 정확히 3→2로 변경했다.
- pointer `click`과 Aurum touch `tap`은 각각 New tab을 실제로 추가해 1→2→3 상태 전이를 만들었다. 이는 status code만이 아니라 screenshot의 count/card postcondition으로 확인했다.
- Aurum health/screenshot/key/click/tap은 동작했지만 tree는 `root_count: 0`이었다. 따라서 semantic accessibility tree는 PASS가 아니다.
- 최종 TPK SHA-256은 `5c2b4a46076f1a82610ce4626cb637550c7e5aa33fe2526cee7e992d58294124`다. Tizen CLI의 `-s` 없는 emulator-test-only signer를 사용했고 archive, manifest, Browser payload, author/distributor signature, update-install, launch를 확인했다.

## 6. 최종 3관점 판정

- Architect: PASS. address/search, page content, navigation, Tabs 관리의 역할이 분리됐고 기능 없는 Samsung command나 새 TV dashboard를 발명하지 않았다.
- Product/visual: PASS(Common Emulator 범위). generic desktop top row와 generic full-width tab row를 제거했으며 Samsung Internet의 page-first, split navigation, card/close/dialog family를 TV 거리와 실제 command 집합에 맞게 번역했다.
- CX/accessibility: PARTIAL. deterministic remote/pointer/touch state postcondition, non-color selected/focus cue, modal trap/Back restoration은 PASS다. semantic tree, non-zero inset native, multi-resolution native, real history navigation은 미검증이다.

## English evidence summary

The refs-first prototype and installed NUI now use a compact address/Reload header, a separate Back/Forward/Tabs dock, and a full-canvas tab manager with preview/title/URL cards. Playwright covered keyboard, pointer, touch, disabled states, modal trapping/restoration, bounds, and four viewport shapes. The final Common Emulator package was update-installed and visually validated at 1920×1080 for Home, a real public HTTPS page, Tabs, and the close dialog; remote close changed the tab count from three to two.

This completes only the Browser visual-fidelity module. Aurum returned an empty accessibility tree, so semantic accessibility remains unverified. Historical Browser/View Action, resolver, and ViewAnnotation `action-tool` E2E used a compatibility experiment that current project policy no longer permits for fresh generation; legacy Display round trips were not part of this Browser package run. Canonical A2UI target rendering remains blocked by the two-string Presentation transport, and the transport-breaking offline experiment was not repeated.
