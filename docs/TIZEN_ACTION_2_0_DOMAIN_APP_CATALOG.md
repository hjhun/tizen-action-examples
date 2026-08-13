# Tizen Action Framework 2.0 예제 도메인 앱 카탈로그

## 목적

이 문서는 `tizen-action-examples`에서 구현할 예제 앱의 범위를 `appfw/tizen-action/default-actions`의 실제 Action/Entity 정의를 기준으로 정리한다. 현재 Calendar 초안이 있으므로, 이후 예제는 단순 UI 샘플이 아니라 **Action provider 발견 → typed RPC → 상태/후속 조회 → UI 또는 system adapter 검증**을 보이는 도메인 앱으로 만든다.

## 분석 기준

- 기준 소스: `<tizen-action-repo>/default-actions`
- 분석 대상: `actions/*.action`, `entities/*.entity`, `action.seq`
- 발견 결과: 공개 도메인 Action category 21개, 도메인 Action 124개, 내부 View Action 4개, Entity schema 46개.
- 각 `.action`의 `details.appid`는 현재 플랫폼/제품 provider의 식별자다. 예제 앱은 해당 appid를 재사용하지 않고 별도의 예제 appid와 provider metadata를 가져야 한다.
- `action.seq`의 category 내 순서는 TIDL method ID이므로, 예제 provider 생성 시 해당 category 전체를 `actionc -a <category>`로 생성한다. 기존 순서를 변경하거나 일부 Action만 생성하여 ID를 다시 매기지 않는다.

> Graphify 사전 점검: Graphify CLI의 `update`를 이 소스에 실행했으나, 현재 설치본은 `.action`/`.entity` 확장자를 코드 입력으로 인식하지 않아 그래프를 만들지 못했다. 따라서 아래 표는 원본 JSON schema와 `action.seq`를 직접 파싱하여 산출했다. Graphify 캐시는 저장소 외부에 둔다.

## 구현 단위 원칙

1. 아래의 **도메인 앱**은 원칙적으로 category당 하나의 독립 예제 프로젝트로 둔다. 한 앱이 여러 category의 일부 Action을 흉내 내는 방식은 provider discovery와 Entity 소유권을 흐린다.
2. `Display`와 `Tizen.Action.View`는 일반 도메인 앱이 아니라 다른 앱을 지원하는 **공통 infrastructure fixture**로 둔다.
3. 이미 Calendar 초안이 있는 경우에도 CRUD, stable-ID resolver, Search, Presentation, persistence/restart 및 알람 보상까지 검증될 때만 완료로 처리한다.
4. 플랫폼 전역 상태를 실제로 바꾸는 category는 예제에서 안전한 in-app simulator/repository를 사용하고, 실제 system adapter는 capability와 권한을 확인할 수 있는 target에서 별도 검증한다.

## 필요한 도메인 앱 목록

| 순서 | 예제 앱/프로젝트 제안 | Action category | Action 수 | 주요 Entity | default-actions의 기존 provider appid | 예제 범위와 최소 완료 시나리오 | 권장 단계 |
|---:|---|---|---:|---|---|---|---|
| 1 | `Calendar` (초안 존재) | `Tizen.Action.Calendar` | 6 | `Calendar`, `Query`, `Presentation`, `Status` | `org.tizen.action-framework.service` | Event Add → GetEventByIds → Search → Update → ToPresentation → Remove 및 restart/alarm 보상 | P0 |
| 2 | `ScheduleReminder` | `Tizen.Action.Schedule` | 10 | `Reminder`, `Reservation`, `Query`, `Status` | `com.samsung.tv.reminder` | reminder CRUD/complete/search와 viewing·recording reservation 생성·취소; persistence와 alarm lifecycle | P0 |
| 3 | `Browser` | `Tizen.Action.Browser` | 5 | `Browser`, `Calendar`, `Presentation` | `org.tizen.next-browser` | URL Go, current-page 조회, GetBrowserByIds, page→calendar 변환, presentation | P0 |
| 4 | `PhotoGallery` | `Tizen.Action.Photo` | 5 | `Photo`, `Query`, `Presentation` | `org.tizen.photo-player-tv` | 사진 add/delete/search, stable-ID lookup, gallery presentation | P0 |
| 5 | `MusicLibrary` | `Tizen.Action.Music` | 17 | `Music`, `MusicFile`, `MusicQuery`, `Album`, `Artist`, `Playlist`, `Station`, `Controller` | `com.samsung.tv.music-flex` | search/play/playlist mutation 및 Album·Artist·Playlist·Station resolver; 가장 넓은 media 상태 모델 검증 | P1 |
| 6 | `VideoCatalog` | `Tizen.Action.Video` | 7 | `Content`, `ContentQuery`, `Files`, `Presentation` | `HEPsqFNie0.tvplusstandalone` | filtered content search, GetContentByIds, details, play/control, directory play, presentation | P1 |
| 7 | `BroadcastGuide` | `Tizen.Action.Broadcast` | 16 | `Channel`, `Program`, `RecordedProgram`, `BroadcastQuery`, `Presentation` | `org.tizen.tv-viewer` | channel/EPG search·tune·record·recording playback, 3개 entity resolver, guide launch | P1 |
| 8 | `IoTHome` | `Tizen.Action.IoT` | 4 | `Device`, `Scene`, `Status` | `com.samsung.tv.SmartThingsApp` | device list/status, bounded device control, scene execution; authorization and command validation | P1 |
| 9 | `SettingsCenter` | `Tizen.Action.Settings` | 4 | `Setting`, `Query`, `Status` | `org.tizen.menu` | setting search/get/set/open; mutation persistence와 type/range validation | P1 |
| 10 | `AppHub` | `Tizen.Action.App` | 4 | `App`, `Query`, `Status` | `com.samsung.tv.store` | installed/running app list, search, launch deep link, store-detail navigation | P1 |
| 11 | `GameHub` | `Tizen.Action.Game` | 3 | `Game`, `Query`, `Status` | `com.samsung.tv.gamehome` | game search/launch와 game-bar open; app launch adapter 경계 | P2 |
| 12 | `HealthCoach` | `Tizen.Action.Health` | 4 | `Workout`, `HealthSummary`, `Query`, `Presentation` | `com.samsung.tv.samsung-health` | workout search/start, daily summary, presentation; sensitive health data 최소화 | P2 |
| 13 | `ArtGallery` | `Tizen.Action.Art` | 4 | `Artwork`, `Query`, `Presentation` | `org.tizen.art-app` | current artwork, search/show, presentation; display 연동 | P2 |
| 14 | `CameraCapture` | `Tizen.Action.Camera` | 5 | `Camera`, `Status` | `com.samsung.tv.UsbCameraApp` | device select/switch, capture-mode open, start/stop; hardware unavailable case 명시 | P2 |
| 15 | `ScreenShare` | `Tizen.Action.ScreenShare` | 2 | `Source`, `Status` | `com.samsung.tv.googlecast-app` | cast/mirroring start/stop, source validation, lifecycle cleanup | P2 |
| 16 | `MultiViewManager` | `Tizen.Action.MultiView` | 13 | `MultiView`, `Screen`, `App`, `Status` | `com.samsung.tv.multiscreen` | split/PIP launch, app placement/removal, focus/fullscreen/size/sound-focus state transitions | P2 |
| 17 | `HomeNavigator` | `Tizen.Action.Home` | 2 | `Home`, `Status` | `com.samsung.tv.csfs` | current page lookup와 page switch; global navigation adapter 분리 | P3 |
| 18 | `AccessibilityControl` | `Tizen.Action.Accessibility` | 2 | `Accessibility`, `Status` | `org.tizen.screen-reader` | feature state 조회/설정; permission, reversible state, assistive UX 검증 | P3 |
| 19 | `DeviceSupport` | `Tizen.Action.Support` | 4 | `DeviceInfo`, `Manual`, `Status` | `org.tizen.emanual-app` | device info, update check, manual page, diagnosis; immutable 정보와 trigger 동작 분리 | P3 |
| 20 | `VolumeControl` | `Tizen.Action.Volume` | 6 | `Volume`, `Status` | `org.tizen.volume-app` | get/set/raise/lower/mute/unmute; range·mute transition·idempotence 검증 | P3 |
| 21 | `DisplayPresentation` | `Tizen.Action.Display` | 1 | `Presentation`, `Status` | `com.samsung.tv.bixbycapsuleviewer` | 다른 provider가 만든 Presentation을 화면에 표시하는 공통 renderer fixture | Infrastructure |

## 공통 infrastructure fixture

| 프로젝트 제안 | Action category | Action 수 | 역할 | 연결 대상 |
|---|---|---:|---|---|
| `ViewContextFixture` | `Tizen.Action.View` | 4 | focused/annotated view 조회, ID lookup, View→Presentation 변환을 제공하는 framework fixture | Agent-facing UI annotation, `DisplayPresentation`, 모든 NUI 도메인 앱 |
| `DisplayPresentation` | `Tizen.Action.Display` | 1 | `Presentation` entity를 실제 화면 또는 deterministic test view로 출력 | `ToPresentation` Action을 가진 Calendar, Browser, Art, Health, Music, Photo, Video, Broadcast |

## 단계별 구현 우선순위

| 단계 | 목표 | 포함 앱 | 선택 근거 |
|---|---|---|---|
| P0 — 상태/Entity 기준선 | typed CRUD, stable ID, resolver, persistence와 Presentation의 기준 확립 | Calendar, ScheduleReminder, Browser, PhotoGallery, ViewContextFixture, DisplayPresentation | Action 2.0의 Agent discovery/Entity refresh에 필요한 핵심 흐름을 가장 작은 독립 도메인으로 증명한다. |
| P1 — 검색·카탈로그·연동 | query/filter, multi-entity resolver, cross-device command, launch adapter | MusicLibrary, VideoCatalog, BroadcastGuide, IoTHome, SettingsCenter, AppHub | catalog/search와 상태 변경의 실제 Action 조합을 넓힌다. |
| P2 — 제품 기능/복합 상태 | hardware/session/multi-surface 상태 모델 | GameHub, HealthCoach, ArtGallery, CameraCapture, ScreenShare, MultiViewManager | TV/product capability 의존성이 커서 Public Common Emulator의 기본 gate 이후로 둔다. |
| P3 — 전역 시스템 제어 | 권한·안전성·되돌림이 필요한 system control | HomeNavigator, AccessibilityControl, DeviceSupport, VolumeControl | 사용자 환경 전체에 영향을 주므로 simulator 우선, target 권한 검증은 별도 gate로 둔다. |

## 모든 도메인 앱의 공통 acceptance checklist

| 구분 | 최소 증거 |
|---|---|
| 생성 | 해당 category 전체를 `actionc -a <category>`로 생성하고 generated service의 모든 abstract method를 compile한다. |
| Provider discovery | manifest/provider metadata 등록 후 `action-tool find-appids <category> --json`에서 예제 appid를 확인한다. |
| Typed Action E2E | advertised Action마다 positive invocation, 하나의 bounded negative case, typed status를 확인한다. |
| 상태 도메인 | mutation 뒤 Search/GetByIds postcondition, stable ID와 request order, restart restoration을 검증한다. |
| UI | NUI가 있을 때 D-pad/pointer focus, editor와 destructive confirmation을 검증한다. |
| 안전성 | 실제 전역/hardware action은 capability·permission·unavailable 결과를 구분하고, simulator가 실제 system state를 변경하지 않게 한다. |
| 플랫폼 범위 | Public Tizen Common Emulator 결과는 Common validation으로만 기록한다. TV profile/product capability 검증은 별도 결과로 기록한다. |

## 범위 밖 항목

- `Tizen.Entity.*` schema 자체를 예제 앱에서 새로 정의하거나 platform `default-actions`를 수정하지 않는다.
- 기존 platform/provider appid를 예제 manifest에 사용하지 않는다.
- generated TIDL C# source를 수동 수정하지 않는다. schema/category 입력을 기준으로 재생성한다.
- 이 문서는 제품 앱의 완전한 대체 구현 목록이 아니라, Action Framework 2.0 계약을 검증하기 위한 독립 예제 provider의 개발 카탈로그다.
