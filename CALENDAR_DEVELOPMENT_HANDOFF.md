# Calendar B UI · CRUD · Reminder 개발 인계 문서

- 마지막 갱신: 2026-08-08 14:07 KST
- 저장소: `/home/hjhun/samba/workspace/tizen-action-examples`
- Calendar 루트: `/home/hjhun/samba/workspace/tizen-action-examples/Calendar`
- 브랜치: `main`
- 대상 장치: `emulator-26101`, Public Tizen 10.1 Common Emulator
- 패키지 ID / 앱 ID: `org.tizen.actionexamples.calendar`
- 상태: **Host tests/build/package 및 Common Emulator install/runtime/Action/View-A2UI GREEN; 실제 D-pad·pointer·screenshot acceptance 미완료**

이 문서는 새 Hermes 세션에서 Calendar 개발을 바로 이어가기 위한 현재 상태, 검증 증거, 명령 및 남은 작업의 단일 인계 지점이다. 완료되지 않은 emulator 작업을 완료로 해석하지 않는다.

---

## 1. 사용자 결정과 제품 목표

사용자는 UI 후보 중 **B안 `One UI TV Split`**을 선택했다.

확정된 기본 화면:

```text
16:9 light surface
  ├─ 좌측 68%: 7열 × 6행 month grid, 총 42개 독립 date cell
  └─ 우측 32%: 선택 날짜 Agenda, event cards, Add event, Reminders
```

최종 사용자 흐름:

```text
Calendar 실행
  → month/Agenda split
  → D-pad 또는 touch로 날짜와 일정 탐색
  → 일정 등록·수정·삭제
  → 일정 연결 알림 10분/30분/1시간/1일 전 설정
  → 독립 Reminder 등록·수정·완료·다시 열기·삭제
  → JSON persistence
  → Tizen Alarm/Notification 예약·취소·재예약
  → 앱 재실행 후 데이터 및 미래 alarm 복원
```

핵심 아키텍처 결정:

- D-pad와 touch/pointer는 별도 business path를 만들지 않는다.
- UI와 Calendar/Schedule provider는 같은 repository 및 `CalendarCommandService` 인스턴스를 사용한다.
- UI는 자기 provider RPC를 호출하지 않는다.
- 기존 `Calendar_GetEventByIds` resolver 계약은 유지한다.
- generated C#은 직접 수정하지 않고 `actionc`로만 생성한다.
- Calendar event interval overlap은 half-open이다.

```text
event.Start < endExclusive
event.End > startInclusive
```

---

## 2. 저장소 상태 및 보존 규칙

2026-08-08 14:07 KST의 `git status --short`:

```text
?? .dev/
?? .hermes/
?? .superpowers/
?? Calendar/
```

주의:

- Calendar tree 대부분이 untracked이므로 `git diff`만 보고 파일이 없거나 변경이 없다고 판단하지 않는다.
- 구현 중 commit, push, reset, clean을 하지 않았다.
- 다음 경로를 보존한다.

```text
.dev/
.hermes/
.superpowers/
Calendar/
```

---

## 3. 현재 구현된 구조

### 3.1 Domain

주요 파일:

```text
Calendar/src/Calendar.Domain/CalendarEvent.cs
Calendar/src/Calendar.Domain/CalendarEventRepository.cs
Calendar/src/Calendar.Domain/CalendarReminder.cs
Calendar/src/Calendar.Domain/CalendarReminderRepository.cs
```

구현 범위:

- stable-ID event create/update/delete
- ordered `ResolveByIds`
- half-open interval query
- thread-safe snapshot/search/mutation
- event-linked reminder
- independent reminder
- reminder complete/reopen
- alarm ID metadata 보존

### 3.2 Persistence

주요 파일:

```text
Calendar/src/Calendar.Persistence/CalendarJsonStore.cs
Calendar/src/Calendar.Persistence/CalendarStoreDocument.cs
Calendar/src/Calendar.App/CalendarJsonPersistenceAdapter.cs
```

구현 범위:

- schema version 1
- event/reminder round trip
- missing-file empty fallback
- unsupported schema 거부
- temp-file 기반 atomic replacement
- 저장 실패 시 기존 파일 보존
- corrupt JSON backup 및 empty recovery
- 앱 data directory의 `calendar-data.json` 사용

### 3.3 Transaction/use-case

주요 파일:

```text
Calendar/src/Calendar.UseCases/CalendarCommandService.cs
```

구현 범위:

- event create/update/delete
- event-linked reminder scheduling
- event update 시 replacement alarm 예약과 기존 alarm 취소
- event delete 시 연결 reminder/alarm 제거
- independent reminder create/update/delete
- reminder complete/reopen
- persistence 성공 뒤 repository publish
- persistence 실패 시 새 alarm compensation cancel
- startup `Restore()`와 alarm reconciliation

### 3.4 NUI UI

주요 파일:

```text
Calendar/src/Calendar.App/CalendarApplication.cs
Calendar/src/Calendar.App/CalendarMonthView.cs
Calendar/src/Calendar.App/CalendarDateCellView.cs
Calendar/src/Calendar.App/SelectedDayAgendaView.cs
Calendar/src/Calendar.App/CalendarOverlayView.cs
Calendar/src/Calendar.App/CalendarEditorState.cs
Calendar/src/Calendar.App/CalendarReminderEditorState.cs
Calendar/src/Calendar.App/CalendarInteractionState.cs
Calendar/src/Calendar.App/CalendarUiState.cs
Calendar/src/Calendar.App/CalendarUiCommand.cs
Calendar/src/Calendar.App/CalendarTouchBinder.cs
```

구현 범위:

- 42개 독립 date cell
- 68:32 month/Agenda split
- Today
- Agenda event card
- focusable/touchable Add event
- focusable/touchable Reminders 진입 surface
- event detail/editor/delete confirmation
- `TextField`/`TextEditor` 기반 title/date/time/location/note 입력
- event reminder preset `{10, 30, 60, 1440}`
- independent reminder list/editor/delete confirmation
- reminder Done/Reopen
- 일정 및 reminder 삭제 confirmation에 대상 이름과 날짜 표시

Agenda D-pad 순서:

```text
Agenda events → Add event → Reminders
```

Back hierarchy:

```text
Event delete confirmation → Event detail
Event editor → Event detail 또는 Calendar
Reminder delete confirmation → Reminder editor
Reminder editor → Reminder list
Reminder list → Calendar
Agenda → Month root
Month root → Exit
```

Pointer activation 계약:

```text
Down 후 Up-inside일 때 정확히 한 번 activate
Down 없는 Up, Up-outside, cancel, 이미 소비된 sequence는 activate하지 않음
```

### 3.5 Tizen Alarm/Notification adapter

파일:

```text
Calendar/src/Calendar.App/TizenReminderAlarmScheduler.cs
```

현재 구현:

- 미래이며 미완료인 reminder만 예약
- `Notification.Tag = calendar-reminder:<id>`
- `AlarmManager.CreateAlarm(reminder.DueAt.LocalDateTime, notification)`
- persisted alarm ID로 특정 alarm 검색 후 `Alarm.Cancel()`

현재 소스 기준 확인 사항:

- `TizenReminderAlarmScheduler`에는 `Reset()`이나 `AlarmManager.CancelAll()` 호출이 없다.
- scheduler는 persisted alarm ID로 해당 alarm만 찾아 `Alarm.Cancel()`한다.
- `CalendarCommandService.Restore()`는 기존 persisted alarm ID를 개별 취소한 뒤 미래·미완료 reminder만 재예약한다.

따라서 이 문서의 과거 `CancelAll()` 미해결 결함 기록은 현재 소스에 해당하지 않는다. 실제 alarm cancel/reschedule의 device-level 확인은 별도 acceptance 항목으로 유지한다.

manifest privilege:

```text
http://tizen.org/privilege/alarm.get
http://tizen.org/privilege/alarm.set
http://tizen.org/privilege/notification
```

### 3.6 Calendar Action provider

주요 파일:

```text
Calendar/src/Calendar.ActionProvider/Generated/CalendarActionProvider.cs
Calendar/src/Calendar.ActionProvider/CalendarService.cs
Calendar/src/Calendar.ActionProvider/CalendarActionProviderHost.cs
```

연결된 Actions:

```text
Tv_Tizen.Action.Calendar_GetEventByIds
Tv_Tizen.Action.Calendar_AddEvent
Tv_Tizen.Action.Calendar_UpdateEvent
Tv_Tizen.Action.Calendar_RemoveEvent
Tv_Tizen.Action.Calendar_Search
Tv_Tizen.Action.Calendar_ToPresentation
```

`GetEventByIds`의 ordered result와 `unresolvedIds` 계약은 변경하지 않았다.

### 3.7 Schedule Action provider

주요 파일:

```text
Calendar/src/Calendar.ScheduleActionProvider/Generated/ScheduleReminderActionProvider.cs
Calendar/src/Calendar.ScheduleActionProvider/ScheduleReminderService.cs
Calendar/src/Calendar.ScheduleActionProvider/ScheduleReminderActionProviderHost.cs
```

연결된 Actions:

```text
Tv_Tizen.Action.Schedule_CreateReminder
Tv_Tizen.Action.Schedule_UpdateReminder
Tv_Tizen.Action.Schedule_DeleteReminder
Tv_Tizen.Action.Schedule_SearchReminder
Tv_Tizen.Action.Schedule_CompleteReminder
```

Schedule category는 `action.seq` method ID ABI를 보존하기 위해 reminder action 5개만 골라 생성하지 않고 **전체 `Tizen.Action.Schedule` category**로 생성했다. Recording/Viewing reservation methods는 명시적인 unsupported status를 반환한다.

---

## 4. Generated source provenance

Calendar와 Schedule generated C#은 임시 디렉터리에 `actionc`로 재생성한 뒤 repository 파일과 `cmp`로 byte-for-byte 비교했다.

검증된 생성 명령 형식:

```bash
export ACTIONC_ACTION2TIDL="$HOME/.local/bin/action2tidl"
export ACTIONC_TIDLC="$HOME/.local/bin/tidlc"

actionc \
  -a Tizen.Action.Calendar \
  -d /home/hjhun/samba/workspace/appfw/tizen-action/default-actions \
  -l 'C#' \
  -o Calendar/src/Calendar.ActionProvider/Generated/CalendarActionProvider

actionc \
  -a Tizen.Action.Schedule \
  -d /home/hjhun/samba/workspace/appfw/tizen-action/default-actions \
  -l 'C#' \
  -o Calendar/src/Calendar.ScheduleActionProvider/Generated/ScheduleReminderActionProvider
```

`-o`에는 `.cs` 확장자를 붙이지 않는다. 붙이면 `.cs.cs`가 생성될 수 있다.

현재 SHA-256:

```text
CalendarActionProvider.cs
4a721f89fcfd78c6b52f3c2c5e97e99a80a637b516ed77267e027c9bdd166376

ScheduleReminderActionProvider.cs
08aa0b1217ffc5fa4fdaadaf17c8674e41fe08842640b8f91d6447a7810b9835
```

---

## 5. Fresh host 검증 결과

마지막 fresh 실행 결과:

```text
Calendar.Domain.Tests: PASS
Calendar.Persistence.Tests: PASS
Calendar.UseCases.Tests: PASS
Calendar.App.Tests: PASS
```

빌드 결과:

```text
Calendar.ActionProvider:         0 warnings, 0 errors
Calendar.ScheduleActionProvider: 0 warnings, 0 errors
Calendar.App:                    0 warnings, 0 errors
```

`git diff --check`도 통과했다.

재실행 명령:

```bash
cd /home/hjhun/samba/workspace/tizen-action-examples

set -euo pipefail
dotnet run --project Calendar/tests/Calendar.Domain.Tests/Calendar.Domain.Tests.csproj
dotnet run --project Calendar/tests/Calendar.Persistence.Tests/Calendar.Persistence.Tests.csproj
dotnet run --project Calendar/tests/Calendar.UseCases.Tests/Calendar.UseCases.Tests.csproj
dotnet run --project Calendar/tests/Calendar.App.Tests/Calendar.App.Tests.csproj
dotnet build Calendar/src/Calendar.ActionProvider/Calendar.ActionProvider.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.ScheduleActionProvider/Calendar.ScheduleActionProvider.csproj --configuration Debug --no-restore
dotnet build Calendar/src/Calendar.App/Calendar.App.csproj --configuration Debug --no-restore
git diff --check
```

`Calendar.ActionProvider.Tests`를 host에서 실행하면 Tizen runtime assembly 부재로 실패할 수 있다.

```text
FileNotFoundException:
Tizen.Applications.Common, Version=4.0.0.0
```

따라서 generated provider runtime behavior는 emulator에서 실제 Action invocation으로 검증해야 한다. Host에서는 domain/use-case behavior와 provider compile을 검증한다.

---

## 6. TPK packaging — profile을 지정하지 않는 public emulator 경로

### 중요 규칙

Public emulator용 generic package에서는 signing profile을 별도로 지정하지 않는다.

올바른 형태:

```bash
tizen package -t tpk -o "$PACKAGE_OUTPUT" -- "$STAGE"
```

사용하지 않을 형태:

```bash
# 사용하지 말 것
tizen package -t tpk -s . ...
tizen package -t tpk -s hjhun ...
```

`-s`를 생략하면 Tizen CLI가 emulator-only 기본 서명을 적용하고 다음 warning을 출력한다.

```text
WARNING: Default profile is used for sign. This signed package is valid for emulator test only.
```

이 결과는 public emulator 테스트용이며 distribution/production certificate 증거가 아니다.

### 실제 성공한 staging 방식

`bin/Debug/net8.0`에는 stale `packaging/` directory가 있을 수 있다. 디렉터리를 재귀 복사하지 말고 **top-level regular files만** 임시 staging root에 복사한다. 그 뒤 root에 manifest를 복사한다.

```bash
cd /home/hjhun/samba/workspace/tizen-action-examples

dotnet build Calendar/src/Calendar.App/Calendar.App.csproj \
  --configuration Debug --no-restore

OUT="$PWD/Calendar/src/Calendar.App/bin/Debug/net8.0"
STAGE="$(mktemp -d /tmp/calendar-flat-stage.XXXXXX)"
PACKAGE_OUTPUT="$(mktemp -d /tmp/calendar-package-output.XXXXXX)"

python3 - "$OUT" "$STAGE" <<'PY'
import os
import shutil
import sys

source, destination = sys.argv[1:]
for name in os.listdir(source):
    path = os.path.join(source, name)
    if os.path.isfile(path):
        shutil.copy2(path, os.path.join(destination, name))
PY

cp Calendar/src/Calendar.App/tizen-manifest.xml \
  "$STAGE/tizen-manifest.xml"

tizen package -t tpk -o "$PACKAGE_OUTPUT" -- "$STAGE"
```

Generic packager는 `.tpk`가 아니라 executable 이름인 다음 파일을 생성했다.

```text
$PACKAGE_OUTPUT/Calendar.App.dll
```

그러나 이 파일은 ZIP 기반 signed TPK payload다. archive를 검증한 뒤 명시적인 `.tpk` 이름으로 복사한다.

```bash
unzip -t "$PACKAGE_OUTPUT/Calendar.App.dll"
mkdir -p Calendar/dist
cp "$PACKAGE_OUTPUT/Calendar.App.dll" \
  Calendar/dist/org.tizen.actionexamples.calendar-0.1.0.tpk
```

### 현재 생성된 package

```text
/home/hjhun/samba/workspace/tizen-action-examples/Calendar/dist/org.tizen.actionexamples.calendar-0.1.0.tpk
```

SHA-256:

```text
5423c05a0588ddab85b988aa241fcd236fc0b20c56da226c266b0503a48b27c9
```

Archive 검증:

```text
ZIP integrity: PASS
entries: 21
missing required entries: none
```

확인된 필수 payload:

```text
author-signature.xml
signature1.xml
tizen-manifest.xml
bin/Calendar.App.dll
lib/Calendar.ActionProvider.dll
lib/Calendar.ScheduleActionProvider.dll
lib/Calendar.Domain.dll
lib/Calendar.Persistence.dll
lib/Calendar.UseCases.dll
lib/Calendar.App
lib/Calendar.App.deps.json
lib/Calendar.App.runtimeconfig.json
```

이 package는 아직 `emulator-26101`에 최신 버전으로 설치·acceptance하지 않았다.

---

## 7. 다음 세션의 시작 순서

### 7.1 코드 안전성 수정

1. 현재 파일과 `git status --short`를 다시 읽는다.
2. `TizenReminderAlarmScheduler.Reset() => AlarmManager.CancelAll()`을 RED→GREEN으로 제거한다.
3. persisted reminder의 기존 alarm ID만 개별 취소하고 미래 reminder를 재예약하도록 `IReminderAlarmScheduler`와 `Restore()`를 수정한다.
4. 전체 host tests/build를 재실행한다.
5. package를 다시 만들고 SHA-256을 갱신한다.

### 7.2 Emulator 준비와 설치

```bash
sdb devices
sdb -s emulator-26101 capability
sdb -s emulator-26101 shell 'id'
sdb -s emulator-26101 install \
  Calendar/dist/org.tizen.actionexamples.calendar-0.1.0.tpk
```

프로필 확인:

```text
manifest profile: common
emulator platform: Tizen 10.1 Common
```

설치 후 launch와 process/log survival을 확인한다. 앱 ID:

```text
org.tizen.actionexamples.calendar
```

### 7.3 Action provider discovery 및 E2E

manifest에 등록된 Calendar/Schedule Actions가 Action DB에서 실제 발견되는지 확인한다. TPK install의 transport 성공만으로 등록 성공을 추론하지 않는다.

Calendar:

```text
Calendar_AddEvent
Calendar_UpdateEvent
Calendar_RemoveEvent
Calendar_Search
Calendar_ToPresentation
Calendar_GetEventByIds
```

Schedule:

```text
Schedule_CreateReminder
Schedule_UpdateReminder
Schedule_DeleteReminder
Schedule_SearchReminder
Schedule_CompleteReminder
```

각 mutation 뒤에는 typed status뿐 아니라 UI와 동일 repository/persistence postcondition도 확인한다.

### 7.4 UI 기능 acceptance

D-pad와 touch/pointer 양쪽으로 다음을 실제 수행한다.

- 날짜 이동: 좌우 ±1일, 상하 ±7일
- 월 경계 통과 시 visible month 변경
- Agenda 진입 및 focus 복귀
- Add event
- event detail
- event update
- event delete confirmation 취소와 확정
- event reminder 10/30/60/1440분 설정
- Reminders 진입
- independent reminder create/update
- Done/Reopen
- reminder delete confirmation 취소와 확정
- Back hierarchy
- `Down → Up-inside` 단일 activation
- Up-outside/cancel non-activation

### 7.5 Persistence/alarm/restart acceptance

1. 일정 및 독립 reminder를 생성한다.
2. alarm ID와 실제 scheduled alarm을 확인한다.
3. event update 시 기존 alarm 취소와 replacement alarm 생성을 확인한다.
4. event/reminder delete 또는 complete 시 해당 alarm만 취소되는지 확인한다.
5. 앱 process를 종료하고 다시 실행한다.
6. event/reminder가 복원되는지 확인한다.
7. 미래 alarm이 reconcile되는지 확인한다.
8. 완료 또는 과거 reminder가 재예약되지 않는지 확인한다.

Common Emulator에서 notification 표시가 제한되면 다음을 분리해서 보고한다.

```text
alarm scheduling evidence
notification display evidence
```

### 7.6 Screenshot acceptance

다음 화면을 fresh capture한다.

- root month/Agenda
- event detail
- event editor 및 reminder presets
- event delete confirmation
- reminder list
- reminder editor
- reminder delete confirmation
- validation/error state

검토 항목:

- B안 68:32 hierarchy 유지
- clipping 없음
- 실제 focus ring 표시
- touch target 누락 없음
- disabled/decorative Add 없음
- 입력 field가 실제 편집 가능
- confirmation에 대상 이름과 날짜 표시

---

## 8. 아직 완료로 선언하면 안 되는 항목

현재까지 확인된 것은 host/domain/build/package layer다. 다음은 미완료다.

- 최신 TPK의 `emulator-26101` 설치
- 최신 앱 launch 및 process/log survival
- 최신 Calendar/Schedule Action DB registration
- 실제 Calendar Action RPC CRUD
- 실제 Schedule Action RPC CRUD/Complete
- emulator D-pad 전체 흐름
- emulator pointer/touch 전체 흐름
- event-linked alarm 실제 예약·취소·재예약
- independent reminder alarm 실제 예약·완료·삭제
- 앱 재실행 persistence 복원
- B안 최신 screenshot visual acceptance

따라서 현재 상태를 다음처럼 보고한다.

```text
Host tests/build: PASS
Generated provenance: PASS
Default emulator-signed TPK packaging: PASS
Emulator install/runtime/action/UI/restart acceptance: NOT YET VERIFIED
```

---

## 9. 관련 설계·계획 문서

```text
.hermes/plans/2026-08-08-samsung-calendar-one-ui-tv-split-design.md
.hermes/plans/2026-08-08_123244-samsung-calendar-one-ui-tv-split-implementation.md
.hermes/plans/2026-08-08-calendar-crud-touch-reminder-design.md
.hermes/plans/2026-08-08_125713-calendar-full-interaction-crud-reminder-implementation.md
```

이 인계 문서와 위 계획을 함께 읽되, 실제 source와 fresh tool output을 최종 진실로 사용한다.

---

## 10. 2026-08-08 fresh Common Emulator E2E 결과

검증 대상은 `emulator-26101`의 Public Tizen 10.1 Common (`x86_64`)이다. 다음 fresh TPK를 source에서 다시 build/flat-stage/package하여 설치했다.

```text
Calendar/dist/org.tizen.actionexamples.calendar-0.1.0-e2e.tpk
SHA-256: 893afbaca59cffca7732a73fb9f93a38edbac425bc9c442d46c0377266cbac14
```

통과한 gate:

- `dotnet build` 0 warning / 0 error, archive integrity 및 required managed payload 확인
- TPK install, application launch, terminate/relaunch, running-process 확인
- Calendar / Schedule / View provider category discovery에서 app ID 발견
- Calendar Add/GetByIds/SearchInPeriod/Update/Remove Action RPC 및 update 뒤 restart persistence 확인
- Schedule Create/Search/Complete/Delete Action RPC 확인
- rendered Calendar ViewAnnotation의 positive bounds, FindById, generated EntityJson 확인
- complete `Tizen.Entity.View` payload로 View ToPresentation을 호출해 A2UI `surfaceUpdate`/`dataModelUpdate` 결과 확인

아직 통과로 선언하지 않는 gate:

- 실제 D-pad 및 pointer/touch 입력 전 경로
- event card actual-focus 뒤 View_GetFocusedView 검증
- screenshot 기반 clipping/focus-ring/68:32 visual acceptance
- alarm scheduling/cancel/reschedule의 device-level 확인

검증 당시 SDB client/server 버전은 4.2.36/4.2.25로 불일치 warning이 있었다. install, launch 및 RPC에는 성공했지만, 후속 입력·화면 검증 전에 버전 정합성을 맞추는 것이 바람직하다.
