# Calendar / Reminder 1단계 검증 기록

검증일: 2026-09-06. 대상: .NET Calendar 및 Reminder의 API 14 migration,
전체 Action category, ViewAnnotation 수명, 현재 Presentation, 해상도 대응.
PhotoGallery는 이 보고서 검토 후 별도 착수 승인을 받는다.

## 판정과 환경

| 검증 계층 | 결과 |
|---|---|
| Host TDD | Calendar 5개 + Reminder 2개 suite PASS |
| 생성 계약 | Calendar 4개 / Reminder 3개 C# 생성물 fresh regeneration byte equality PASS; 상속 구현 |
| API 14 Release build | 두 앱 0 warnings / 0 errors |
| TPK | 두 앱 manifest/API 14/payload/Custom resources/서명 파일 및 설치 PASS |
| action-tool | Calendar 37개 + 정리/삭제 후조건 6개, Reminder 33개 PASS |
| View/Presentation | 앱별 FHD 25개, UHD 24개, DCI 4K 24개 검사 PASS |
| Native UI | FHD/UHD/DCI 4K 렌더·실측, D-pad/포인터 및 대표 편집·확인·복원 PASS |
| 8K | Host geometry TDD PASS. **Unverified on target due to emulator DRM constraint** |
| TV/product | 미검증. 예약은 Common Emulator app-owned simulator |

- `tizenfx`: Gerrit `tizen_10.1`, `6837507e05e22a4f5f4e76f532bd42b583d26709`,
  Release 14.0.0.19360 (2026-09-04). 2026-09-06 fetch 후 clean checkout 확인.
- 앱 reference: `Tizen.NET 14.0.0.19326`, .NET SDK 8, manifest .NET API 14.
  공개 `Information.TryGetValue`, NUI Window/GetInsets/FocusManager/측정 API만 사용한다.
- authoritative default-actions: `appfw/tizen-action/default-actions`.
  플랫폼 schema를 수정하거나 생성 코드의 null serializer/privilege 코드를 패치하지 않았다.
- FHD: `emulator-26111` / `tc-0905-actionagent` / Public Common x86_64,
  Tizen 10.1 Unified 20260905.101134, Action runtime/tool 1.3.27.
- UHD/DCI/8K 시도: 이 작업이 만든 `provider-resolution-0906`, serial `emulator-26101`,
  같은 reference image의 격리 VM. 검증 후 종료·삭제 및 포워드 제거.
- 고해상도 VM의 오래된 Action runtime에는 로컬 GBS 1.3.27의
  amd-mod-tizen-action, tizen-action, capi-appfw-tizen-action을 정합화했다.
  같은 NEVRA의 다른 빌드가 있어 replacepkgs/replacefiles가 필요했다.
  오래된 building-block dependency 메타데이터 때문에 모듈 설치에 nodeps를 사용했다.
  이는 폐기 가능한 시험 VM 준비이며 앱 코드의 Interop 우회가 아니다.
- 고해상도 VM에는 FHD에서 검증한 새 action-tool binary를 별도
  `/usr/bin/provider-action-tool`로 배치했다. 기존 catalog를 직접 편집하지 않았다.

## 구현과 계약

Calendar는 native 구현을 제거하고 전체 Calendar 5개, Reminder 5개, View 4개 및
CalendarCustom 2개(GetEventByIds/SearchInPeriod)를 제공한다.
Reminder는 전체 Reminder 5개와 View 4개, ReminderCustom 6개를 제공한다.
기존 Schedule 프로젝트 이름은 내부 이름으로 남지만 Schedule Action은 광고하지 않는다.
Custom category의 runtime fallback은 알파벳 순서다. 현재 v1 순서를 생성·검사하고
표준 category는 전체 action.seq를 사용한다.

표준 Reminder State의 To-do/In-progress/Blocked/Done을 저장·복원한다.
구 bool Completed 데이터와 stable ID를 유지한다. Category는 도메인 필터이며
Completed 같은 UI smart-list 이름은 거부한다. batch resolver는 요청 순서와
중복을 보존하고 unresolved IDs를 별도로 반환한다.

UI와 Provider는 같은 repository/use-case를 사용한다. 실패 DTO의 모든 문자열과
nested entity를 adapter에서 초기화하여 생성 serializer의 null crash를 해소했다.
예약 Repeat은 표준 Entity에 있는 once/daily/weekly를 사용한다.
Weekdays는 현재 표준 wire enum에 없으므로 거부하며 실방송 작업으로 가장하지 않는다.

## 해상도 실측

초기 SystemInfo screen capability와 실제 NUI WindowSize/GetInsets를 각각 읽는다.
이 emulator의 screen feature는 큰 window에서도 1280×720이었다. 실제 drawable
window에 따라 1920×1080 canvas 한 곳에서 uniform ancestor transform을 적용한다.
하위 UI 수치는 디자인 단위이며 typography/입력/border/radius/focus가 한 번 확대된다.
resize/inset은 기존 canvas를 변환하여 저장 전 입력과 포커스를 유지한다.
zero/non-finite/underflow scale 및 소진된 inset은 기존 frame을 유지한다.

| 실제 native window | 배율 | 측정 page ScreenBounds X/Y/W/H (두 앱) | 캡처 |
|---|---:|---|---|
| FHD 1920×1080 | 1 | 0 / 0 / 1920 / 1080 | 두 앱 PNG 1920×1080 |
| UHD 3840×2160 | 2 | 0 / 0 / 3840 / 2160 | 두 앱 PNG 3840×2160 |
| DCI 4K 4096×2160 | 2 | 128 / 0 / 3840 / 2160 | 두 앱 PNG 4096×2160, 좌우 128 여백 |
| 8K 7680×4320 | host 4 | host 예상 0 / 0 / 7680 / 4320 | native 미검증 |

UHD/DCI Reminder Today focus의 실제 크기는 522.24×150.96이며 X가
138.88에서 266.88로 128만큼 이동했다. Calendar date focus는 같은 두 해상도에서
339.676×256.813으로 유지되어 DCI를 가로로 늘리지 않음을 확인했다.
FHD 가상 키보드가 연 inset에서도 편집 화면이 축소되고 입력/저장이 유지됐다.
1280×720, 4:3, ultrawide, 비대칭 inset 및 큰 screen/작은 window는 별도 host cases다.

8K 시도에서는 3072 MiB RAM/512 MiB VIGS VRAM으로 kernel framebuffer
7680×4320까지 생성됐다. `/sys/class/drm/card0-LVDS-1/modes`는 비어 있었고,
Enlightenment가 `e_output.c:1938 fail to get tmodes`, `e_display_init failed`로
종료되어 Aurum screen RPC가 timeout됐다. 앱 실행 전 graphics mode 제약이다.
사용자가 이 제한의 명시와 단계 마무리를 승인했다. 크기만 큰 framebuffer나
host 계산을 native UI 통과로 세지 않았다.

## Action 성공·실패와 후조건

| 제공 메서드 | 성공 / 유의미한 실패 / 후조건 |
|---|---|
| Calendar AddEvent / UpdateEvent / DeleteEvent | 생성·수정·삭제 / 중복·잘못된 입력·없는 ID / Search 및 resolver 내용·삭제 확인 |
| Calendar Search / CalendarCustom SearchInPeriod | ID·기간·선택 필드 / 잘못된 기간·bounded query / 예상 ID·내용 |
| CalendarCustom GetEventByIds | 순서·중복·미해결 / oversized IDs / 동일 ID 순서 및 unresolved |
| Calendar ToPresentation | 실제 Search 배열 / 잘못된 Entity·크기 초과 / 실제 Display 표시 |
| 두 앱 Reminder Add / Update / Delete | 생성·4 state·삭제 / 충돌·잘못된 값·ID / Search state 및 빈 결과 |
| 두 앱 Reminder Search / ToPresentation | 도메인 query·현재 상태 표시 / query·미존재 / persisted state·실제 Display |
| ReminderCustom GetReminderByIds | 중복/순서/미해결 / 101개 초과 / 요청 순서와 unresolved |
| ReminderCustom AddViewing / AddRecording | 미래 예약 / end≤start / GetReservations에 각각 owned ID |
| ReminderCustom CancelViewing / CancelRecording | 해당 kind 취소 / 반대 kind / 취소 ID 제거·다른 ID 유지 |
| ReminderCustom GetReservations | 실제 예약 조회 / 없는 provider route / 추가·각 취소 후 4개 조회 대조 |
| View FindById / ToPresentation | 현재 ID·현재 데이터 / missing·oversized·mismatch / live store와 실제 표시 |
| View GetAnnotatedViews / GetFocusedView | 실제 rendered 목록·focus / pause 시 빈 목록·미포커스 / 다시 foreground 후 current ID |

GetAnnotatedViews/GetFocusedView는 두 앱에서 없는 provider route 실패를 각각 확인했다.
DisplayPresentation 표시 중에는 두 앱의 목록이 비고 GetFocusedView가 typed failure를
반환하는 pause 후조건도 확인했다. 입력이 없는 조회에는 존재하지 않는 provider route 또는 실제 비활성 UI 상태를
사용했다. GetAnnotatedViews의 비활성 빈 목록은 유효한 성공(empty state)이며
typed failure로 허위 집계하지 않는다. 공개 API에 없는 오류 인자를 만들지 않았다.

재현 도구: `Calendar/tests/prepare_target_validation.py`,
`Reminder/tests/prepare_target_validation.py`, `Reminder/tests/check_target_results.py`,
`scripts/validate-provider-views.py`. JSON wire는 generated dispatch와 실제
`action-tool execute --json` / `action-tool run --json` 결과로 확인했다.

## View 수명·입력·Presentation

- Focus/input/layout 변경은 측정 가능한 tree를 즉시 원자적으로 게시한 후
  50ms/relayout에서 다시 측정한다. root 교체 및 pause/terminate만 비운다.
  이전 Clear→timer 사이의 조회 공백을 제거했고 앱별 20회 연속 list/find를 통과했다.
- View_ToPresentation은 View ID/EntityType/EntityId로 현재 store에서 찾는다.
  위조·오래된 caller EntityInfo를 신뢰하지 않는다. identity mismatch/제거된 ID는
  실패하며 유효한 현재 ID는 최신 snapshot을 사용한다.
- Calendar Month/Week/Day/Agenda/Search/detail/new/edit/delete 흐름,
  빈 제목 오류, 키보드 실제 입력 Q→저장→공개 Search→삭제→빈 Search를 검증했다.
  Week 날짜 heading/연도 clipping과 confirmation 줄바꿈을 수정했다.
  overlay 배경 pointer 입력 차단과 Close 후 기존 Week 복원을 확인했다.
- Reminder 실제 키보드 입력 T→저장→공개 Search→Complete(Done),
  삭제 dialog의 Cancel 초기 focus/Right→Delete/Back 취소 및 focus 복원,
  terminate/relaunch 후 같은 ID/Done 유지와 최종 삭제 후 빈 조회를 검증했다.
- FHD 페이지 View→Presentation→설치된 DisplayPresentation 왕복 앱별 25 checks,
  Entity detail의 실제 최신 상태 표시, Action-to-display도 통과했다.
  renderer를 Dismiss한 뒤 원 앱을 다시 foreground하여 검증한다.
- wire profile은 **legacy A2UI v0.8** split `surfaceUpdate` Template /
  `dataModelUpdate` Document이다. canonical v0.9.1 또는 투명 overlay 지원을 주장하지 않는다.

## 증거와 정리

[Calendar UI_PARITY](UI_PARITY.md)와 [Reminder UI_PARITY](../../Reminder/docs/UI_PARITY.md)에
현재 native PNG, preview 비교, 입력과 해상도 provenance를 기록했다.
Aurum accessibility tree는 root_count=0이었다. remote key 및 native 좌표 클릭과
실제 View bounds를 사용했으며 semantic tree 탐색/물리 touch를 검증했다고 하지 않는다.
공용 홈 화면 경고는 캡처 전에 닫았고, Back/Home overlay는 플랫폼 요소로 남겼다.

빌드 산출물, TPK, raw BGRA, 임시 scenario/result/log는 커밋하지 않는다.
안정 경로의 native PNG와 재현 가능한 소스/검증 문서만 포함한다.
DisplayPresentation, docs/DASHBOARD.md, graphify-out의 기존 미커밋 작업은
별도 변경으로 보존한다. 전체 작업 트리가 clean하다는 주장을 하지 않고
Calendar/Reminder 단계 범위와 staged artifact 목록을 별도로 점검한다.
