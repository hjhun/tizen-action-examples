# Presentation RPC null 문자열 회귀 검증

## 원인과 현재 판정

2026-09-06 22:11:08 +0900의
`org.tizen.actionexamples.calendar_309594_20260906221108.zip`은
`/opt/usr/share/crash/dump/`에 보관된 Calendar SIGABRT(6) 기록이다.
실행파일은 `/usr/bin/dotnet-hydra-loader`, managed 진입점은 이전 앱 ID 아래의
`bin/Calendar.App.dll`이다. 다음 managed 스택이 dump 로그에 있다.

```text
ArgumentNullException: Value cannot be null. (Parameter 'chars')
System.Text.UTF8Encoding.GetByteCount
Tizen.Applications.RPCPort.Parcel.WriteString
TizenActionCalendar.Serialize(TizenEntityPresentation) :1504
UnitMap.Write :1147 → Unit.Serialize :996 → UnitMap.Serialize :1034
OnReceivedEvent :1777/:1597 → RPCPort.StubBase.OnReceivedEvent
```

Action 요청 이름 `Tv_Tizen.Action.Calendar_ToPresentation`은 crash 직전
22:11:07.402 및 22:11:08.135 로그에 확인된다. **요청 JSON 인자는 확보되지
않았다.** 잘못된 Entity 또는 범위 초과 입력이었다는 설명은 추정이며,
정확한 당시 입력을 재현했다고 주장하지 않는다. 확인된 직접 원인은 반환된
Presentation의 null 문자열을 생성 serializer가 `WriteString`에 전달한 것이다.
native `.info`의 제한된 libc/CoreCLR 심볼보다 위 managed 스택을 우선한다.

기존 수정 커밋 `e0a7e9a0558b6a70d4537526a18726589b4c58b2`는
`CalendarService.ToPresentation` 진입 시 `Template`과 `Document`를 빈 문자열로
초기화한다. 그 전 committed 구현에는 실패 시 `new TizenEntityPresentation()`을
반환하는 경로가 있었다. 다만 crash 당시에는 새 CalendarEvent/custom 계약이 이미
로그에 나타나므로 이전 committed 소스나 저장소의 2026-08-08 TPK가 crash 바이너리와
동일하다고 단정할 수 없다. 당시 미커밋 migration 빌드의 정확한 출처는 미확인이다.

현재 `CalendarService`, `ScheduleReminderService`, `CalendarViewService`는 실패
분기 전에 두 문자열을 초기화하고, 성공 시 JSON serializer 또는 검증된 snapshot
builder의 문자열을 설정한다. Calendar의 null 목록/잘못된 Entity/100개 초과/출력
크기 초과, View의 annotation 누락/identity 불일치/지원하지 않는 snapshot 반환을
소스에서 확인했다. 이번 작업에서는 운영 코드나 생성 코드를 수정하지 않았다.
과거 결함을 만들기 위해 운영 코드를 되돌리는 red 실행도 하지 않았다.

앱 ID 변경은 별도 커밋 `91293ea`이며, 자세한 사항은
[앱 ID migration 기록](APP_ID_MIGRATION.md)을 참고한다.

## 재실행

이미 설치된 Calendar를 실행한 상태에서 다음 순서로 검증한다. 설치는 대상과
데이터 보존 범위를 확인한 뒤 별도로 수행한다. 다른 작업자가 같은 Calendar를
수정 중이면 전후 동일성 검사가 실패할 수 있으므로 검증 시간을 조율한다.

```bash
dotnet run --project Calendar/tests/Calendar.ActionProvider.Tests -c Release
dotnet run --project Calendar/tests/Calendar.Domain.Tests -c Release
dotnet run --project Calendar/tests/Calendar.App.Tests -c Release
./Calendar/build.sh
./Calendar/package.sh
sdb -s emulator-26101 install Calendar/dist/org.tizen.calendar-0.1.0-api14.tpk
sdb -s emulator-26101 shell 'app_launcher -s org.tizen.calendar'
python3 Calendar/tests/check_presentation_rpc.py \
  --serial emulator-26101 --output /tmp/calendar-presentation-regression-new
```

출력 디렉터리는 새 경로여야 한다. `calls.json`은 실제 wire 요청/응답을 저장하고,
`report.json`은 전후 PID, crash 목록, UTC 관측 시각과 판정을 저장한다. 이 출력에는
실제 일정이 포함되므로 로그·dump·백업·TPK와 함께 커밋하지 않는다. 검사는 이벤트를
생성·수정·삭제하지 않는다. 유효하지 않은 값과 큰 입력은 변환 전용 fixture다.

Host 테스트는 도메인/adapter/snapshot 검증이며 Tizen RPC serializer 실행을
증명하지 않는다. 그 부분은 설치본의 `action-tool execute --json -f`가 검증한다.
긴 JSON을 SDB shell 인라인 인자로 전송하면 `error: service name too long`이
발생할 수 있다. 동일 JSON을 target 파일로 전송하고 `-f`로 읽게 한다.

## 2026-09-08 검증 결과

대상은 `emulator-26101`, `tc-0905-actionagent`, Tizen 10.1 Unified x86_64,
build `tizen-10.1-unified_20260905.101134_tizen-headed-emulator64-wayland`이다.
Calendar는 `org.tizen.calendar` 0.1.0, package API10.0 / dotnet API14로 설치했다.
소스 기준은 `1ef5e9b`이며 Tizen.NET 참조를 변경하지 않았다.

| 계층 | 관측 결과 |
|---|---|
| Host | ActionProvider/Domain/App 3 suite PASS; 아래 Restore 확인 후 UseCases도 PASS |
| Build | Release, 0 warnings / 0 errors |
| Package | Common Emulator 전용 서명, ZIP/manifest/signature/payload 검사 PASS |
| Calendar 설치 | 업데이트 성공; 설치 직후 calendar-data.json byte equality, 실행 후 alarm handle만 변경 |
| RPC | 최종 12 calls PASS, 아래의 실패도 transport 성공 + typed failure |
| 후조건 | Search 전후 동일, resolver 결과/순서 일치, 미해결 ID 없음 |
| Crash | PID 11870 유지, 관측 사이 새 crash 파일 없음; 최종 dlog에 해당 PID의 unhandled/SIGABRT 표식 없음 |

Calendar TPK SHA256:
`5a4bb88cf505d2572bc7cf191a9c036934e9fc17463d295d5134a3d8054d7b51`.
설치된 `Calendar.ActionProvider.dll`과 새 패키지 payload의 SHA256은
`f622b40a12ac37346ba4933b7ee3d13af779ff5519688324b9a9ff3d97a19edb`로 일치했다.

실행 후 최종 JSON은 기존 reminder 하나의 `alarmId`만 달랐다. 기존
`CalendarApplication.OnCreate` → `CalendarCommandService.Restore`가 소유한 알람을
취소·재등록하는 동작과 일치한다. 설치 직후의 byte equality와 실행 후의 상태를
구분한다. 해당 handle을 제외한 모든 저장 값은 동일했고 후속 Reminder Search도
성공했다. 이 차이를 검토하면서 UseCases suite를 추가 실행했다. 이전 handle이나
백업 파일을 덮어써 현재 알람을 되돌리지 않았다.

첫 9-call 관측은 00:04:07–00:04:11 KST, 최종 12-call 관측은
00:08:19–00:08:27 KST이다. 첫 관측부터 최종 왕복까지 PID는 11870이었다.
crash 목록에는 기존 2026-09-06 Calendar ZIP이 그대로 남아 있다.
이 제한된 관측을 장시간 안정성 검증으로 확대하지 않는다.

| 실제 변환 입력/경로 | typed 결과 |
|---|---|
| `CalendarEvent`에 현재 Search의 5개 Entity | Success=true, JSON 문자열 |
| `CalendarEvent: []` | Success=true, 빈 목록 Presentation |
| 변환 전용 Entity의 `Title: ""` 또는 end=start | Success=false, 유효 ID/title/range 요구 reason |
| 유효 Entity 101개 | Success=false, `At most 100 calendar events are allowed.` |
| 각각 Note 4096자의 유효 Entity 20개 | Success=false, `Presentation Template and Document must each fit within 64 Ki characters.` |
| Reminder의 빈 제목/잘못된 DueDate | Success=false, Reminder 입력 검증 reason |
| View의 빈 EntityId | Success=false, `A current annotated view is required.` |
| 존재하지 않는 View/Entity identity | Success=false, 현재 visible identity 불일치 reason |

모든 위 실패에서 `result`는 `{"Document":"","Template":""}`였다.
typed 실패 후에도 후속 RPC가 정상 응답하여 원래 null 직렬화 crash가 재발하지 않았다.

## 현재 상태 Presentation 왕복

별도 승인된 기존 renderer TPK만 설치했다:
`DisplayPresentation/dist/org.tizen.displaypresentation-0.1.0-api14.tpk`, SHA256
`f319a78c4c5573c9040b589fc7ddfc7d495561bf3e23ad8adb177a065dbc3490`.
설치 직전 hash를 재확인했다. package/app ID는 `org.tizen.displaypresentation`,
version 0.1.0, common, package API10.0 / dotnet API14다. DLL PE machine 0x14c는
managed PE 메타데이터이며 native x86 전용 실행 증거로 해석하지 않는다.
**출처 commit이 미확인인 기존 산출물**이며 소스를 수정하거나 재빌드하지 않았다.
이 실행을 현재 renderer 소스의 재현 가능한 릴리스 검증으로 부르지 않는다.

- `find-appids Tizen.Action.Presentation --json`이 설치 후 해당 ID를 반환했고
  Show 이후 앱이 running이었다.
- Calendar Search → ToPresentation → Show는 실제 5개 Entity 배열을 사용했다.
  Aurum에서 1/5 페이지의 첫 일정 제목/시간과 Next focus만 관측했다.
  나머지 4개 페이지의 시각적 검증은 주장하지 않는다.
- Calendar를 foreground로 복원하여 `GetAnnotatedViews`와 `FindById`로 실제
  Month 일정 chip을 조회했다. 별도의 `GetFocusedView`는 9월 8일 date cell을
  반환했다. 선택한 일정 chip이 focused였다는 주장은 하지 않는다.
- 해당 일정 View의 `Tizen.Entity.CalendarEvent` snapshot을 `View_ToPresentation`에
  전달했다. 반환 Document의 ID/title/time/location/note가 원 Entity와 일치했다.
  그 결과를 Show에 전달하여 Aurum에서 9월 8일 15:00–16:00 일정 내용과
  Dismiss focus를 확인했다. renderer 자신의 View로 Calendar 왕복을 대체하지 않았다.
- Aurum health는 1920×1080, accessibility tree는 root_count=0이었다.
  실제 native screenshot과 Action ViewAnnotation으로 검증했다.

검증 profile은 **legacy A2UI v0.8 split surfaceUpdate/dataModelUpdate**다.
canonical v0.9.1, 투명 overlay, 다른 해상도, physical TV 또는 전체 UI acceptance를
검증한 것이 아니다. renderer Back은 내용을 비우며 Calendar 복원은 명시적
app_launcher로 수행했다. 자동 포커스 복귀를 주장하지 않는다.

원시 증거는 검증 호스트의 `/tmp/p5-calendar-investigation/`에 별도 보관했다:
`rpc-final/calls.json`, `rpc-final/report.json`, `calendar-selected-body.json`,
`calendar-view-presentation-response.json`, `view-show-response.json`,
`calendar-restored-stable.png`, `action-display.png`, `calendar-view-display.png`.
스크린샷은 1920×1080 PNG로 decode 확인했다. 이 임시 경로는 배포 문서의
스크린샷 링크가 아니며 장기 보존을 보장하지 않는다. 재검증 시 위 절차로 새 증거를 만든다.
