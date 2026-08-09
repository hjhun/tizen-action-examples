# Samsung Android Browser UI 참조와 Tizen 번역

조사일: 2026-08-09
주 참조 제품: Samsung Browser for Android
확인된 공개 앱 버전: `30.0.0.67` (Google Play 갱신일 2026-07-08)
One UI 버전: 공식 지원 화면에 명시되지 않아 추정하지 않음

## 출처 기록

| ID | 공식 출처 | 화면/내용 | 직접 확인 범위 | 버전·날짜 한계 |
|---|---|---|---|---|
| SAM-BROWSER-001 | [Samsung Browser — Google Play](https://play.google.com/store/apps/details?id=com.sec.android.app.sbrowser) | 제품 기능, privacy posture, 게시자, current stable version | Samsung Electronics 게시, v30.0.0.67, 2026-07-08 update를 직접 확인 | 스토어 screenshot과 지원 screenshot의 정확한 build 대응은 명시되지 않음 |
| SAM-BROWSER-002 | [Use the navigation bar in Samsung Browser](https://www.samsung.com/us/support/answer/ANS10012955/) | Galaxy S26의 page/toolbar | 주소 검색, Reload, Back, Forward, Home, Browsing assist, Bookmarks, Tabs, Menu를 직접 확인 | 페이지가 앱 version/One UI version을 명시하지 않음 |
| SAM-BROWSER-003 | [Open or close tabs in Samsung Browser](https://www.samsung.com/us/support/answer/ANS10012961/) | Tabs list/select/new/close, close-all dialog | thumbnail+title+URL card, selected outline, per-tab X, New tab, `Cancel`/`Close` 확인을 직접 확인 | 개별 tab close의 확인 dialog 여부는 직접 확인되지 않음; close-all만 확인됨 |
| SAM-BROWSER-004 | [How to use Secret mode in Samsung Browser](https://www.samsung.com/us/support/answer/ANS10010475/) | Secret mode, 별도 tabs, address indicator, settings | history/cookie/search 비저장 설명, mask indicator, dark treatment, screenshot setting을 직접 확인 | 이번 Tizen 범위에는 Secret mode를 구현하지 않음 |
| SAM-BROWSER-005 | [Use the Samsung Browser app](https://www.samsung.com/us/support/answer/ANS10001594/) | privacy dashboard와 browser tools | Privacy dashboard, delete/reset confirmation 존재를 직접 확인 | 해당 screen의 정확한 current app build는 명시되지 않음 |
| SAM-BROWSER-006 | [Samsung Browser overview](https://developer.samsung.com/internet/overview.html) | 삼성 제품군의 실제 web browser 역할 | Galaxy/Smart TV 등에서 web browser를 제공한다는 제품 경계를 직접 확인 | 시각 세부 규격을 제공하지 않음 |
| SAM-BROWSER-007 | [One UI Design Guidelines](https://design.samsung.com/global/contents/one-ui/download/oneui_design_guide_eng.pdf) | hierarchy/toolbar/dialog 일반 원칙 | browse surface toolbar와 action hierarchy의 일반 원칙 확인 | 오래된 일반 가이드이며 current Browser 화면보다 보조 근거로만 사용 |

Samsung 공식 screenshot은 시각 감사 목적으로 임시 확인했으며 proprietary asset을 저장소에 복사하지 않는다.

## 화면별 Android → Tizen 번역

| Samsung Browser 화면/요소 | 직접 검증된 Android 행동 | 1920×1080 Tizen 적용 | 검증/추론 구분 |
|---|---|---|---|
| Page + navigation toolbar | 웹 콘텐츠가 대부분을 차지하고 주소, Back, Forward, Home, Tabs, Menu가 browser navigation을 구성 | real `WebView`를 가장 큰 content-only region으로 두고, 상단 command band에 Back/Forward/Reload/address/Tabs 배치 | 제어 의미는 직접 검증; 상단 배치와 크기는 TV adaptation |
| Address/search | 하나의 주소 영역으로 URL 탐색/검색 | 1044×66 design-unit 편집 영역, 초기 focus, Enter submit; URL이 아니면 privacy-safe search URL | 통합 주소/검색은 직접 검증; 검색 endpoint/geometry는 adaptation |
| Loading | Reload와 page surface가 navigation context를 유지 | address/chrome을 보존하고 5px progress band, latest-intent state 표시 | loading 시각 세부는 공식 screenshot에서 직접 검증되지 않아 명시적 제품 adaptation |
| Tabs list | 별도 Tabs screen, thumbnail/title/URL, selected outline, X close, New tab | thumbnail은 remote page capture 대신 local placeholder/색 block; 2-column TV list/card가 아니라 한눈에 순서를 읽는 wide rows | 화면 hierarchy/selected/close는 직접 검증; thumbnail 제거와 row geometry는 privacy/TV adaptation |
| Close all dialog | dimmed background, rounded modal, Cancel/Close, destructive red label/button | 개별 tab close도 오작동 비용 때문에 동일한 modal family 사용; initial focus Cancel, Back cancel | dialog family는 직접 검증; 개별 tab confirm은 Tizen remote safety adaptation |
| Secret mode | 별도 tab set, mask indicator, dark address treatment, 비저장 privacy contract | 이번 scope에서 제공하지 않음. normal-only label과 문서/Action privacy boundary로 오해 방지 | 미구현을 명시; Secret mode처럼 보이는 UI 금지 |
| Privacy dashboard | tracker/privacy state를 별도 surface에서 설명 | account/tracker dashboard는 범위 밖. 인증/권한/엔진 오류는 자동 승인 없이 bounded unavailable state | privacy priority는 직접 검증; 구체적 unavailable UI는 adaptation |
| More/Menu | secondary actions는 메뉴에 집약 | 이번 scope에서는 구현되지 않은 기능을 장식 menu로 노출하지 않음 | 직접 검증된 IA를 범위 축소에 적용 |
| Back behavior | Android system Back과 browser Back이 screen/history를 되돌림 | modal → Tabs → WebView history → app 순서로 한 계층씩 처리 | Android 개념은 직접 검증; 구체적 D-pad order는 adaptation |

## 시각 언어

- 직접 검증: 흰/밝은 neutral surface, 콘텐츠 중심, 얇은 divider, rounded address/controls, selected tab의 blue outline, destructive action의 red, dimmed modal 배경.
- Tizen adaptation: TV 거리에서 읽히는 24~52px급 typography, 52px product safe margin, 4~5px focus outline와 1.025 scale이라는 두 cue, 넓은 hit target, reduced motion 지원.
- 금지: Samsung 명칭/logo/icon asset 복제, proprietary thumbnail/remote media, 임의 gradient/glass/floating dock, generic dashboard, desktop multi-tab strip.

## Music translation exemplar에서 차용한 방법과 제외 사항

`Music/refs/music-design.html`은 읽기 전용으로 검사했다. 1920×1080 centered uniform canvas, TV-distance hierarchy, compact contextual chrome, content-first surface, outline+surface/scale focus, deterministic transition이라는 방법만 채택한다. rose token, font, media, gradient, branding, player/library controls, domain data, 정확한 geometry는 차용하지 않는다.
