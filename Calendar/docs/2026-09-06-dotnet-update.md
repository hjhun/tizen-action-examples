> 이 문서는 마이그레이션 중간 기록입니다. API 14 타깃 설치·Action·View·UI 최종 결과와 Reminder 마이그레이션은 [1단계 검증 기록](STAGE1_VALIDATION.md)을 따릅니다.

# .NET Calendar 갱신 — 2026-09-06

[English](2026-09-06-dotnet-update_Eng.md)

## 범위와 기준

.NET/NUI Calendar의 화면 크기 대응과 Action catalog 갱신, Calendar·Reminder의 페이지별 ViewAnnotation을 수정했습니다. 요청에 따라 `Calendar/native`를 제거했습니다. 별도 Reminder 앱의 Schedule/API 13 계약은 이번 annotation 수정의 범위에서 유지합니다.

- Action source: `~/samba/workspace/appfw/tizen-action/default-actions`, revision `4355566aa851470dc290c69672ba1f8bdd8470e7`, 2026-09-06 확인.
- TizenFX source: `~/tizen/platform/core/csapi/tizenfx`.
- Calendar reference: `Tizen.NET 14.0.0.19326`, dotnet manifest API 14. API 14 `HasPrivilegeLocal`을 포함한 생성물을 그대로 사용합니다.
- 기존 Samsung Calendar에 기반한 Month/Week/Day/Agenda 구조를 유지합니다. 새 디자인이나 삼성 제품 버전 검증을 추가한 작업은 아닙니다. 기존 2026-08-08 screenshot은 이번 변경의 시각 검증 증거가 아닙니다.

## Screen과 Window

`Tizen.System.Information.TryGetValue()`의 `http://tizen.org/feature/screen.width`, `screen.height`와 `Window.Default.WindowSize`를 각각 읽습니다. 실제 drawable area는 window에서 `GetInsets()`를 뺀 값입니다. 물리 screen 크기를 window 크기로 간주하지 않습니다.

1920×1080 reference canvas를 중앙 정렬하고 단일 ancestor의 uniform scale을 적용합니다. 폰트는 reference `PixelSize`로 지정합니다. FHD/4K UHD/8K UHD 전체 window의 scale은 1/2/4이며, 8K screen의 FHD window는 1입니다. 두 축을 따로 늘리거나 화면 너비만으로 글자를 확대하는 방식은 작은 window에 맞지 않아 선택하지 않았습니다.

화면 정보가 없으면 유효한 window만 사용합니다. 잘못된 window/inset에는 기존 root를 유지하며 screen으로 대체하지 않습니다. resize는 기존 page/overlay canvas 위치와 scale을 바꾸므로 저장 전 텍스트·포커스가 유지됩니다. 좌표는 layout 후 다시 측정합니다.

## Action 변경

| 이전 | 현재 |
|---|---|
| `Tizen.Entity.Calendar` | `Tizen.Entity.CalendarEvent` |
| `Calendar_RemoveEvent` | `Calendar_DeleteEvent` |
| `Calendar_Search(Query)` | `Calendar_Search(CalendarQuery)` |
| `Query.Number` | `Query.Limit`, Id/Category 필터 반영 |
| 표준 `Calendar_GetEventByIds` | `CalendarCustom_GetEventByIds` |
| 표준 `Calendar_SearchInPeriod` | `CalendarCustom_SearchInPeriod(Calendar.Entity.SearchQuery)` |
| 단일 Calendar ToPresentation | CalendarEvent 배열 0–100개 |
| Calendar 내부 Schedule reminder methods | 표준 Reminder Add/Delete/Update/Search/ToPresentation |
| Completed bool wire | `ReminderState.State`: To-do/In-progress/Blocked/Done |

이전 wire ABI를 유지하지 않습니다. consumer도 현재 schema로 생성해야 합니다. 기존 저장 ID와 bool completion JSON은 유지하며 4개 state가 저장·복원됩니다.

표준 CalendarQuery.Id로 단일 ID 검색은 가능합니다. Custom resolver는 최대 100개 요청의 순서·중복과 unresolved IDs 반환을 위해 필요합니다. Custom SearchQuery는 CalendarQuery를 상속하고 title/location/note 선택자를 추가합니다. 모두 false이면 전체 필드 검색입니다. 표준 category는 전체 `action.seq` 순서로 생성하고 Custom category는 따로 생성합니다. platform schema와 generated C#을 수동 수정하지 않습니다.

Custom v1 계약의 Action 파일 형식은 v2입니다. definition, entity, provider metadata와 `res/`의 원문 파일을 함께 패키징합니다. UI와 provider는 동일 repository/use-case를 사용합니다.

## 페이지와 포커스

[ViewAnnotation 계약](VIEW_ANNOTATION.md)에 전체 페이지와 검증 절차를 기록했습니다. 페이지와 컨트롤은 generated `TizenEntity`의 `Extra`에 version 1 상태를 저장합니다. 일정/리마인더는 해당 generated 표준 Entity를 사용합니다. 실제 편집 값은 컨트롤에 연결하며 저장된 Entity로 가장하지 않습니다.

Calendar overlay에서는 가려진 배경을 제외합니다. Reminder editor에서는 계속 보이는 목록은 유지하고 사라진 detail은 제외합니다. 페이지 전환·focus·입력·layout·pause/resume에 따라 snapshot을 교체합니다. 동일 일정이 여러 위치에 보이면 별도 View ID를 사용하고 Entity ID는 공유합니다.

Presentation은 현재 Entity/페이지 필드에 바인딩된 기존 legacy v0.8 profile입니다. canonical v0.9.1 및 DisplayPresentation target round trip을 검증했다는 의미가 아닙니다.

후속 개발에서는 DisplayPresentation이 실제 Calendar 0/1/2/7/100개 출력의
explicitList와 중첩 binding을 처리하고, 화면당 텍스트 네 개씩 표시하도록 연결했습니다.
일정 수와 별개로 Template/Document 각각 65,536자를 넘으면 typed failure를 반환합니다.
세 앱의 호스트·빌드·패키지 결과와 실제 target 요청 생성기는
[연동 후속 기록](STAGE1_VALIDATION.md)을 참고하십시오.

## 검증과 남은 단계

| 계층 | 결과 |
|---|---|
| Host | Calendar 5개 + Reminder 2개 suite 통과. 화면 비율, 4K/8K, screen/window 불일치, 상태 저장, query/ID, 페이지 전환·focus·snapshot 수명 검증 |
| 생성 계약 | 전체 method ID/manifest 검사 및 4개 생성 파일의 재생성 byte 일치 |
| Build | 두 .NET 앱 Release build 통과 |
| Package | Calendar API 14 Common Emulator 시험용 서명 TPK의 ZIP·manifest·서명·Custom res 검사 통과 |
| Target Action/UI | 이번 변경은 미설치. 실제 provider 호출·4K/8K 렌더링·입력·Aurum·Presentation round trip 미검증 |

Calendar에서 `./build.sh all`, `python3 tests/check_action_contracts.py`, `./package.sh`를 사용합니다. 패키지는 `dist/org.tizen.actionexamples.calendar-0.1.0-api14.tpk`입니다. native 크기/포커스의 실제 검증은 설치가 승인된 target에서 수행해야 합니다. commit/push는 하지 않았습니다.
