# Reminder 앱 기능·비기능 요구사항 — Approved B

- 문서 상태: **Approved — B Focused Workspace, 2026-08-09**
- 대상: Tizen Action Framework 2.0 `Tizen.Action.Schedule` 예제 provider 앱
- UI reference: Samsung Galaxy Reminder의 정보구조와 상호작용 패턴
- 기준 HTML: [`../refs/reminder-design-options.html`](../refs/reminder-design-options.html)
- 승인 방향: B `Focused Workspace` + A의 smart filter

## 1. 최상위 결정

Reminder 앱은 Samsung Reminder의 빠른 분류·생성 패턴을 참고하되, 16:9 Tizen 화면에서는 **좌측 smart list, 중앙 reminder list, 우측 detail/editor**로 구성된 remote-first workspace를 기본 shell로 사용한다. `Tizen.Action.Schedule`의 Reminder와 TV viewing/recording Reservation은 같은 provider가 소유하지만, 사용자 정보구조에서는 서로 다른 최상위 영역으로 분리한다.

## 2. 배경과 근거

### 2.1 저장소 및 framework 계약

- `Tizen.Action.Schedule`은 `action.seq`에 정의된 10개 Action 전체를 하나의 category로 생성해야 한다.
- Reminder Action: Create, Update, Delete, Complete, Search.
- Reservation Action: AddRecording, AddViewing, CancelRecording, CancelViewing, GetReservations.
- `Tizen.Entity.Reminder`: base `Id`, `Extra` + `Title`, `DueDate`, `Note`, `Completed`.
- `Tizen.Entity.Reservation`: base `Id`, `Extra` + `Channel`, `Program`, `StartTime`, `EndTime`, `Repeat`, `Kind`.
- `Tizen.Entity.Query`: `Keyword`, `Category`, `Number`.
- 기존 platform provider app ID `com.samsung.tv.reminder`는 예제에서 재사용하지 않는다.
- generated Action/Entity source는 수동 수정하지 않는다.

### 2.2 Samsung Reminder reference에서 채택할 패턴

확인 가능한 화면·walkthrough에서 다음 interaction model을 채택 후보로 삼는다.

- Today, Scheduled/Upcoming, Important, Overdue, No alert, Completed 등 smart 분류의 빠른 접근.
- 홈에서 category/smart list를 바로 발견하고, 필요하면 접어 content 영역을 넓히는 구조.
- 하나의 생성 화면에 title, date/time, repeat, category, checklist 등 도구를 집중하는 흐름.
- 검색과 overflow 메뉴의 일관된 상단 배치.
- 빠른 추가와 상세 편집을 별도 페이지 왕복 없이 연결하는 방식.

단, category, priority, checklist, location, attachment, cloud sync는 현재 공개 Reminder Entity 계약에 포함되지 않는다. HTML reference에서 제품 방향을 보여줄 수는 있으나 MVP 구현 요구사항에는 별도 승인 없이 포함하지 않는다.

### 2.3 근거의 한계

- 일부 Samsung 공식 support URL과 Galaxy Store 페이지는 지역 제한, redirect 또는 404로 실제 내용 확인이 제한되었다.
- 최신 One UI 8 화면 세부 동작은 제3자 walkthrough 관찰이며 Samsung의 공식 API·제품 계약으로 간주하지 않는다.
- Tizen Action wire format과 target 알람 동작은 generated dispatch source와 Emulator E2E에서 다시 검증해야 한다.

## 3. 사용자와 핵심 작업

### 3.1 사용자

1. **일반 사용자**: 해야 할 일을 빠르게 생성하고 오늘/예정/완료 상태를 확인한다.
2. **TV 사용자**: remote/D-pad로 reminder와 viewing/recording reservation을 탐색·관리한다.
3. **AI Agent**: Action discovery를 통해 reminder/reservation을 생성·검색·갱신하고, UI의 실제 Entity를 ViewAnnotation으로 식별한다.
4. **개발·검증 담당자**: provider discovery, typed RPC, persistence, alarm lifecycle, UI 상태를 독립적으로 검증한다.

### 3.2 핵심 사용자 여정

1. Reminder 생성 → 목록에 즉시 표시 → due alarm 등록 → Action Search로 확인.
2. Today/Upcoming/Overdue/Completed 탐색 → reminder detail 확인.
3. reminder 편집 → persisted state와 alarm을 일관되게 교체 → 검색으로 후속 확인.
4. reminder 완료 → active 목록에서 제거/Completed에 표시 → alarm 취소 → Undo 가능.
5. reminder 삭제 → 확인 → 저장·alarm 제거 → 목록과 annotation에서 제거.
6. keyword/state 검색 → 결과 선택 → 동일 detail/editor 흐름 진입 → Back 시 검색 위치 복원.
7. viewing/recording reservation 추가 → Reservations에서 확인 → 취소 → GetReservations로 후속 확인.
8. 외부 Action mutation → foreground UI가 동일 repository 변경을 반영 → focus와 annotation이 stale 상태를 노출하지 않음.

## 4. 범위

### 4.1 P0 — HTML 및 첫 구현에 필요한 범위

- Today, Upcoming, Overdue, Completed, All reminders smart list.
- Reminder list, detail, create/edit surface.
- title, due date/time, note, completed 상태.
- one-shot due alarm과 restart restoration.
- reminder CRUD, complete, keyword/state search.
- viewing/recording reservation list, add/cancel.
- empty/loading/validation/persistence/alarm-unavailable/error 상태.
- D-pad, pointer, keyboard, touch 가능한 동일 semantic command path.
- rendered reminder/reservation ViewAnnotation과 focused-view 일치.

### 4.2 P1 — 승인 후 확장 후보

- Important/priority.
- app-owned category.
- repeat reminder.
- checklist.
- snooze.
- quick-add preset.
- category 관리와 recycle bin.

### 4.3 명시적 비범위

- Samsung Account, Microsoft To Do, cloud sync와 협업 category.
- location-based reminder와 geofence.
- image/file/voice attachment.
- 플랫폼 기존 provider app ID 재사용.
- 실제 방송 tuner·recording backend 제어. Public Common Emulator에서는 app-owned reservation simulator를 사용한다.
- platform `default-actions` schema 변경 및 generated source 수동 편집.
- Samsung 상표, icon, screenshot, proprietary asset 복제.

## 5. 정보구조 및 화면 요구사항

### 5.1 기본 shell

- 좌측 navigation: Today, Upcoming, Overdue, Completed, All reminders, Reservations.
- 중앙 content: 선택 smart list의 reminder/reservation 목록.
- 우측 context pane: 선택 항목 detail 또는 create/edit form.
- 상단 command 영역: 현재 범위명·개수, 검색, 접을 수 있는 filter, overflow.
- 우측 하단 Add는 현재 선택 영역에 따라 Add reminder 또는 Add reservation을 실행한다.

### 5.2 화면과 상태

| 화면/표면 | 주요 내용 | 초기 focus | Back 동작 |
|---|---|---|---|
| Reminder list | smart list, filter, reminder cards | 첫 actionable reminder, 없으면 Add | 앱 종료 또는 상위 shell |
| Reminder detail | title, due, note, status, Complete/Edit/Delete | 주요 상태 action | 원래 list card |
| Create/Edit | title, due date/time, note, Save/Cancel | title | 변경 시 discard 확인, 아니면 원위치 |
| Search | query, state filter, results | query | 원래 smart list와 focus 복원 |
| Completed | 완료된 reminder, Restore/Delete | 첫 완료 항목 | 이전 영역 |
| Reservations | viewing/recording 구분, program/channel/time/repeat | 첫 reservation, 없으면 Add | 이전 영역 |
| Confirmation | delete/cancel/discard 확인 | 안전한 Cancel | 호출 control |
| Failure | 원인, Retry/Close | Retry 또는 안전한 Close | 호출 surface |

### 5.3 Visual direction

- content-first, 밝은 neutral surface, restrained violet accent.
- 큰 heading, 명확한 section label, 카드 기반 목록.
- focus는 최소 두 개 cue(3px 이상 outline + background/elevation/scale)를 함께 사용한다.
- overdue는 색상만 사용하지 않고 `Overdue` text/state icon을 함께 제공한다.
- 완료 항목은 checkbox state, text treatment, completed label을 함께 제공한다.
- 1920×1080을 기준 viewport로 삼되 1280×720에서도 horizontal scroll 없이 핵심 task를 완료할 수 있어야 한다.

## 6. 기능 요구사항

### 6.1 탐색과 목록

- **FR-NAV-001** 앱 시작 시 Today를 기본 영역으로 열고, 오늘 미완료 reminder를 due time 오름차순으로 표시한다.
- **FR-NAV-002** Today, Upcoming, Overdue, Completed, All reminders, Reservations를 독립 focus target으로 제공한다.
- **FR-NAV-003** smart list 이동 시 중앙 목록, count, empty state, filter를 원자적으로 갱신한다.
- **FR-NAV-004** 선택 항목은 우측 detail pane에 표시하고 stable Entity ID로 focus를 복원한다.
- **FR-NAV-005** 목록 정렬은 due time을 우선하며 tie는 stable ID로 결정해 결과가 deterministic해야 한다.
- **FR-NAV-006** 외부 Action으로 상태가 바뀌면 현재 화면을 갱신하되 사용자의 입력 중인 draft를 자동으로 덮어쓰지 않는다.

### 6.2 Reminder 생성·조회·편집

- **FR-REM-001** title은 필수이며 trim 후 빈 값이면 저장하지 않고 title에 오류와 focus를 제공한다.
- **FR-REM-002** 사용자는 title, due date/time, note를 입력해 reminder를 생성할 수 있다.
- **FR-REM-003** due가 미래이면 app-owned one-shot alarm을 등록한다.
- **FR-REM-004** due가 없으면 No alert reminder로 저장할 수 있으나 alarm을 등록하지 않는다.
- **FR-REM-005** 생성 성공 시 stable ID를 부여하고 동일 ID를 update/complete/delete/persistence/annotation에 사용한다.
- **FR-REM-005A** UI 생성은 앱이 stable ID를 만든다. CreateReminder Action은 생성 결과가 Status뿐이므로 caller-supplied `Entity.Id`를 필수로 요구한다. 동일 ID·동일 payload 재호출은 중복 항목/알람 없는 idempotent success, 동일 ID·다른 payload는 conflict failure다.
- **FR-REM-006** detail에서 reminder를 편집할 수 있으며 ID는 바뀌지 않는다.
- **FR-REM-007** due 변경 시 기존 snapshot을 보존하고 replacement alarm을 먼저 준비한 뒤 desired state를 atomic 저장하고 in-memory snapshot을 publish한다. 그 후 이전 app-owned alarm만 제거하며, 실패 시 이번 작업에서 새로 만든 handle만 보상 취소한다.
- **FR-REM-008** Save 성공 시 list/detail을 즉시 갱신하고 성공 feedback을 제공한다.
- **FR-REM-009** unsaved edit에서 Back/Cancel 시 discard confirmation을 제공한다.

### 6.3 완료·복원·삭제

- **FR-STATE-001** 미완료 reminder를 완료할 수 있으며 active alarm을 취소하고 Completed 영역으로 이동한다.
- **FR-STATE-002** UI 완료 직후 제한된 feedback window에서 non-modal Undo를 제공한다. Undo는 같은 use-case의 명시적 보상 command이며 일반 Update Action의 `Completed=false`로 암묵 구현하지 않는다.
- **FR-STATE-003** feedback window가 끝난 Completed reminder의 일반 Restore/Reopen은 P0 범위 밖이다. UpdateReminder는 completed 상태를 false로 되돌리지 않는다.
- **FR-STATE-004** 삭제는 confirmation 뒤 수행하며 app-owned alarm, persisted reminder, rendered annotation을 제거한다.
- **FR-STATE-005** 삭제 실패 시 목록에서 항목을 제거하지 않고 Retry/Cancel을 제공한다.

### 6.4 검색

- **FR-SEARCH-001** `Query.Keyword`로 title과 note를 case-insensitive 검색한다.
- **FR-SEARCH-002** `Query.Category`는 P0에서 `all`, `today`, `upcoming`, `overdue`, `completed`, `no-alert` state filter로 해석한다. unsupported 값은 invalid input으로 반환한다.
- **FR-SEARCH-003** `Query.Number`는 result limit이며 1 이상의 bounded 값만 허용한다.
- **FR-SEARCH-004** 결과 순서는 state group, due time, stable ID로 deterministic해야 한다.
- **FR-SEARCH-005** no-result state는 query 수정과 Add reminder recovery action을 제공한다.
- **FR-SEARCH-006** 검색 result 선택은 일반 detail/editor path를 재사용하고 Back 시 query, scroll, focus를 복원한다.

### 6.5 Reservation

- **FR-RES-001** Reservations는 Reminder smart list와 별도 영역으로 표시한다.
- **FR-RES-002** viewing과 recording reservation을 명확한 text/icon label로 구분한다.
- **FR-RES-003** add 시 Channel, Program, StartTime, EndTime, Repeat, Kind를 검증한다.
- **FR-RES-003A** AddViewing/AddRecording Action은 생성 Entity를 반환하지 않으므로 caller-supplied stable reservation ID를 필수로 요구하며 Reminder 생성과 같은 idempotency/conflict 정책을 적용한다.
- **FR-RES-004** Repeat는 schema enum `once`, `daily`, `weekly`, `weekdays`만 허용한다.
- **FR-RES-005** Kind는 `viewing`, `recording`만 허용하며 Action 이름과 input Kind가 불일치하면 invalid input이다.
- **FR-RES-006** StartTime은 EndTime보다 빨라야 하며 과거 reservation은 추가하지 않는다.
- **FR-RES-007** cancel은 stable reservation ID와 Kind가 일치하는 app-owned 항목에만 적용한다.
- **FR-RES-008** GetReservations는 upcoming app-owned reservation을 StartTime, stable ID 순으로 반환한다.
- **FR-RES-009** Public Common Emulator 구현은 실제 tuner/recording state를 변경하지 않는 simulator임을 UI와 문서에 명시한다.

### 6.6 Action provider

- **FR-ACT-001** category 전체를 authoritative `action.seq` 순서로 생성하고 10개 generated abstract method를 모두 구현한다.
- **FR-ACT-002** provider는 validation, generated Entity↔domain 변환, use-case 호출, typed result mapping만 담당한다.
- **FR-ACT-003** Create/Update/Delete/Complete/Search는 UI와 동일 repository/use-case를 사용하며 self-RPC를 하지 않는다.
- **FR-ACT-004** 각 mutation은 success와 failure를 Status로 반환하며 invalid input, not found, conflict, unavailable, internal failure를 일관된 Reason 정책으로 구분한다.
- **FR-ACT-004A** 현재 Status가 `Success`와 `Reason`만 제공하므로 stable Reason prefix(`invalid:`, `not_found:`, `conflict:`, `unavailable:`, `internal:`)로 실패 종류를 구분하고 contract test로 고정한다. 구조화된 status code 추가는 별도 schema 변경 과제다.
- **FR-ACT-005** Action Create 성공 뒤 Search, Reservation Add/Cancel 성공 뒤 GetReservations로 postcondition을 검증할 수 있어야 한다.
- **FR-ACT-006** 실제 wire format은 generated dispatch code와 Emulator invocation 결과를 기준으로 문서화한다.

### 6.7 Persistence와 alarm lifecycle

- **FR-PER-001** reminder, reservation, completed state, app-owned alarm handle을 app-private persistence에 저장한다.
- **FR-PER-002** 저장 파일은 schema version을 가지며 malformed/unsupported data를 명시적 recovery state로 처리한다.
- **FR-PER-003** restart 시 미래 미완료 reminder만 reschedule한다.
- **FR-PER-004** completed, deleted, past-due reminder는 restart에서 reschedule하지 않는다.
- **FR-PER-005** stale alarm handle을 발견하면 app-owned 범위에서 replacement를 만들고 새 handle을 persistence에 반영한다.
- **FR-PER-006** 앱이 소유하지 않은 alarm이나 reservation을 조회·변경·취소하지 않는다.
- **FR-PER-007** persistence 실패는 UI와 Action 모두 명시적으로 보고 silent success를 반환하지 않는다.

### 6.8 ViewAnnotation와 agent-facing UI

- **FR-VIEW-001** 실제 렌더된 reminder card와 reservation card만 해당 generated Entity snapshot으로 annotate한다.
- **FR-VIEW-002** navigation, filter, Add button, empty-state action은 Reminder/Reservation Entity로 annotate하지 않는다.
- **FR-VIEW-003** EntityType, stable EntityId와 generated `ToJson()`을 일관되게 사용한다. 목록 annotation은 실제 카드에 표시된 title/due/completed만 채운 generated Reminder projection을 직렬화하고 note는 detail에 실제 표시될 때만 포함한다.
- **FR-VIEW-004** smart list, search, completion, delete, overlay, app lifecycle 변화에서 stale annotation을 제거한다.
- **FR-VIEW-005** 실제 NUI focus와 focused ViewAnnotation은 같은 stable Entity ID를 가리켜야 한다.
- **FR-VIEW-006** bounds는 실제 rendered geometry에서 수집하고 synthetic zero bounds를 완료 증거로 사용하지 않는다.
- **FR-VIEW-007** foreground에 실제 표시된 canonical Entity 정보만 게시하고 unrelated UI state를 포함하지 않는다.

## 7. 비기능 요구사항

### 7.1 사용성과 접근성

- **NFR-UX-001** 첫 실행 사용자는 Today 목록에서 3개 이하의 activation으로 create surface에 도달해야 한다.
- **NFR-UX-002** reminder title 입력 뒤 due preset 또는 No alert를 선택하고 3단계 이내에 저장할 수 있어야 한다.
- **NFR-UX-003** pointer와 remote activation은 동일 semantic command를 dispatch해 중복 실행 차이를 만들지 않는다.
- **NFR-A11Y-001** 모든 actionable control에 localized accessible name, role, state를 제공한다.
- **NFR-A11Y-002** focus 순서와 directional transition은 결정적이고 keyboard Tab 순서와 의미적으로 일치한다.
- **NFR-A11Y-003** modal/overlay는 focus를 trap하고 닫힐 때 호출 control로 복원한다.
- **NFR-A11Y-004** color contrast는 WCAG 2.2 AA를 목표로 하고 text와 focus indicator를 실제 token 조합으로 측정한다.
- **NFR-A11Y-005** 오류·완료·overdue·disabled 상태는 색 외의 text/icon/shape cue를 제공한다.
- **NFR-A11Y-006** loading, empty, error 상태가 focus dead end를 만들지 않는다.
- **NFR-A11Y-007** pointer/touch target은 최소 44×44 CSS px 상당을 확보하고, TV 주요 control은 60×60 px 상당의 hit surface를 목표로 한다.

### 7.2 성능과 반응성

- **NFR-PERF-001** 일반 list navigation과 focus 이동은 target에서 눈에 띄는 frame stall 없이 반응해야 한다.
- **NFR-PERF-002** 1,000개 reminder에서도 search/filter가 입력 완료 후 200ms 목표 내에 결과 model을 생성해야 한다. 실제 NUI render 시간은 별도 측정한다.
- **NFR-PERF-003** 목록은 bounded page/window를 사용해 모든 Entity view를 동시에 렌더하지 않는다.
- **NFR-PERF-004** Action input title, note, keyword, result count와 batch size에 명시적 상한을 둔다.

### 7.3 신뢰성·일관성

- **NFR-REL-001** UI callback과 Action provider의 동시 mutation에서 repository state가 손상되지 않아야 한다.
- **NFR-REL-002** persistence, in-memory publish, alarm side effect의 순서와 compensation을 host test로 검증한다.
- **NFR-REL-003** 같은 complete/delete/cancel 요청의 재실행은 안전한 typed result를 반환하고 unrelated state를 변경하지 않는다.
- **NFR-REL-004** app restart 뒤 Entity ID, completed state, reservation, valid alarm schedule이 유지되어야 한다.

### 7.4 보안·개인정보

- **NFR-SEC-001** persistence는 app-private 경로를 사용하고 title/note를 log에 평문으로 남기지 않는다.
- **NFR-SEC-002** Action input을 신뢰하지 않고 length, enum, date/time, ID ownership을 provider boundary에서 검증한다.
- **NFR-SEC-003** annotation은 현재 foreground에 렌더된 canonical Entity 범위로 제한한다.
- **NFR-SEC-004** external path, attachment, cloud credential을 P0에서 취급하지 않는다.

### 7.5 유지보수성과 이식성

- **NFR-MAINT-001** domain/use-case는 Tizen runtime 없이 host test 가능해야 한다.
- **NFR-MAINT-002** NUI, Action provider, persistence, scheduler adapter는 inward dependency를 지킨다.
- **NFR-MAINT-003** generated source, domain, UI, platform adapters, tests를 별도 project/directory로 분리한다.
- **NFR-PORT-001** Public Common Emulator 검증과 TV product 검증을 별도 결과로 보고한다.
- **NFR-PORT-002** 1920×1080과 1280×720에서 horizontal overflow 없이 핵심 flow를 완료해야 한다.

### 7.6 관찰 가능성과 테스트 가능성

- **NFR-OBS-001** mutation result, validation category, persistence/alarm failure를 구조화하되 user content는 redaction한다.
- **NFR-TEST-001** 각 advertised Action마다 positive case 1개와 bounded negative case 1개 이상을 E2E로 검증한다.
- **NFR-TEST-002** host test, build, packaging, provider discovery, typed RPC, UI acceptance, ViewAnnotation/A2UI 결과를 서로 다른 gate로 보고한다.

## 8. 데이터 규칙과 invariants

1. reminder/reservation ID는 생성 후 바뀌지 않는다.
2. active reminder는 `Completed=false`이고 deleted state가 아니다.
3. Completed reminder에는 active app alarm이 없다.
4. future due + active reminder만 alarm을 가질 수 있다.
5. due가 없는 reminder에는 alarm이 없다.
6. `Kind=viewing` reservation은 Viewing Action으로, `Kind=recording`은 Recording Action으로만 mutate한다.
7. reservation의 `StartTime < EndTime`이다.
8. UI list, Action Search/GetReservations, persistence, ViewAnnotation은 같은 canonical domain snapshot에서 파생된다.
9. pointer, D-pad, keyboard, touch는 같은 semantic command와 validation을 사용한다.
10. view rerender 뒤 logical focus, actual NUI focus, focused annotation은 같은 semantic ID를 가진다.

## 9. 오류·복구 요구사항

| 상황 | 사용자 경험 | Action 결과 | 복구 |
|---|---|---|---|
| 빈 title | inline 오류, title focus | invalid input | 수정 후 Save |
| 잘못된 date/time | 해당 field 오류 | invalid input | 수정 후 재시도 |
| ID not found | 항목 유지/새로고침 안내 | not found | 목록 refresh |
| alarm unavailable | 저장 정책을 명확히 알림 | unavailable 또는 명시적 partial-policy 금지 | Retry/No alert로 수정 |
| persistence 실패 | 성공처럼 목록 갱신 금지 | internal failure | Retry/Cancel |
| corrupted store | recovery 화면 | provider unavailable/internal | backup/초기화 선택 |
| empty search/list | 설명 + Add/검색 수정 action | success + empty result | query 변경/생성 |
| external update conflict | draft 보존 + 충돌 안내 | typed conflict 지원 여부 검증 | Reload/Keep draft |

## 10. HTML prototype acceptance

- 세 설계안 A/B/C를 같은 URL에서 전환할 수 있다.
- 기본 선택은 B이며 선택 상태가 text와 visual cue로 표시된다.
- B에서 좌측 navigation, 중앙 reminder list, 우측 detail, 검색, filter, Add가 보인다.
- focus card는 outline과 shadow/background의 두 cue로 구분된다.
- 1280px viewport에서 horizontal overflow와 component overlap이 없다.
- HTML은 mockup이며 실제 Samsung asset이나 screenshot을 포함하지 않는다.

## 11. 구현 완료 gate

### 11.1 Host tests

- validation, stable ID, deterministic sorting/filtering.
- create/update/complete/restore/delete transition.
- persistence ordering, alarm compensation, restart restoration.
- reservation kind/time/repeat validation과 cancellation ownership.
- concurrent UI/Action repository access.
- ViewAnnotation publication, stale clearing, focused ID.

### 11.2 Build/package

- 변경 전 baseline build.
- whole-category Action binding generation reproducibility.
- zero new compiler error/warning 목표.
- signed TPK 생성과 manifest/payload 확인.
- `git diff --check`.

### 11.3 Common Emulator E2E

- TPK install 후 fixture manifest를 `unified-backend --preload -y <package-id>`로 명시적 등록.
- provider discovery에서 예제 app ID 확인.
- 10개 Action 각각 positive + bounded failure 호출.
- mutation 뒤 Search/GetReservations postcondition.
- process restart 뒤 persistence/alarm restoration.
- D-pad/pointer/keyboard 핵심 흐름.
- actual focus, ViewAnnotation, A2UI/Presentation 경로.
- 최신 TPK의 실제 렌더 screenshot 증거.

TV profile/product-specific tuner·recording 검증은 별도 후속 gate로 기록한다.

## 12. HTML 설계안 비교

| 안 | 장점 | 단점 | 판단 |
|---|---|---|---|
| A Galaxy Hub | 최신 Galaxy smart list 발견성, one-page 접근 | 상단 카드 수가 많아 D-pad 비용 증가 | filter pattern만 채택 |
| B Focused Workspace | remote focus 결정성, list/detail 동시 확인 | 작은 viewport 대응 필요 | 기본 shell 권장 |
| C Timeline Board | 대형 화면에서 기간 비교 우수 | 완료·검색·편집 흐름과 맞지 않음 | 보조 view 후보 |

## 13. 승인된 결정

1. **정보구조**: B 기본 shell을 채택했다. Today/Upcoming/Overdue/Completed/All/Reservations를 좌측 smart navigation으로 제공한다.
2. **P0 기능**: 공개 Reminder Entity 범위(title/due/note/completed), optional no-alert reminder, one-shot alarm seam에 집중한다.
3. **Reservation 노출**: 같은 앱의 별도 `Reservations` 영역으로 분리하며 Common에서는 deterministic simulator임을 표시한다.
4. **P1 확장**: priority/category/repeat/checklist/snooze는 이번 구현 범위 밖이다.
5. **문구와 locale**: 첫 구현은 영문 product copy와 한국어 설계 문서를 유지한다.
6. **No alert 정책**: optional DueDate를 허용한다.
7. **완료 되돌리기**: Complete는 idempotent이며 일반 Restore/Reopen은 P0에서 제외한다.

구현·빌드·E2E 절차는 [`BUILD_E2E_GUIDE.md`](BUILD_E2E_GUIDE.md)를 따른다.
