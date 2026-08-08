# Reminder 앱 요구사항 및 아키텍처 검토

## 1. 문서 목적과 결론

이 문서는 새 `Reminder` 예제 앱의 구현 전 요구사항 기준선이다. 제품 경험은 Samsung Galaxy Reminder의 **할 일 중심 목록, 빠른 완료, 명확한 편집/삭제, 예정·완료 분리**를 참고하되, Galaxy 화면이나 자산을 복제하지 않고 Tizen NUI와 TV 입력 환경에 맞게 재구성한다.

핵심 결론은 다음과 같다.

1. 앱은 하나의 제품 UI를 제공하지만 도메인에서는 `Reminder`와 `Reservation`을 별도 aggregate로 유지한다.
2. `Tizen.Action.Schedule`의 기존 10개 Action 계약을 모두 지원한다. Action 스키마와 Entity 스키마는 수정하지 않는다.
3. UI와 Action provider는 동일한 Tizen-free command/query service를 사용한다. UI가 자기 provider를 RPC로 호출하지 않는다.
4. generated Entity/TIDL 타입은 Action 및 ViewAnnotation 경계에만 두고, 화면·저장소·알람 로직은 앱 소유 모델로 구현한다.
5. 저장 상태가 source of truth다. 알람/예약 job은 재구성 가능한 외부 파생 자원이며 앱 소유 handle만 조작한다.
6. Public Common Emulator에서는 viewing/recording을 실제 방송·녹화 기능으로 주장하지 않고 deterministic simulator로 검증한다. TV 제품 기능은 별도 gate다.
7. ViewAnnotation은 현재 실제로 렌더된 Reminder/Reservation 의미 단위에만 게시하며, actual NUI bounds/focus와 generated `ToJson()` snapshot을 사용한다.

## 2. 근거와 제약

### 2.1 기준 소스

- `default-actions/action.seq`의 `Tizen.Action.Schedule` 순서
- `actions/Tv_Tizen.Action.Schedule_*.action` 10개
- `entities/Tizen.Entity.Reminder.entity`
- `entities/Tizen.Entity.Reservation.entity`
- `entities/Tizen.Entity.Query.entity`
- `entities/Tizen.Entity.Status.entity`
- `entities/Tizen.Entity.View.entity`
- 저장소의 `AGENTS.md`와 도메인 앱 개발 가이드
- `Calendar/`는 경계와 검증 방식의 참고 구현일 뿐 복제 대상이 아니다.

### 2.2 고정 계약

`action.seq` 순서는 다음과 같으며 재정렬하거나 subset 생성으로 method ID를 다시 매기면 안 된다.

1. `AddRecording`
2. `AddViewing`
3. `CancelRecording`
4. `CancelViewing`
5. `CompleteReminder`
6. `CreateReminder`
7. `DeleteReminder`
8. `GetReservations`
9. `SearchReminder`
10. `UpdateReminder`

전체 category를 `actionc -a Tizen.Action.Schedule`로 생성하고, generated source는 수동 수정하지 않는다. Manifest에는 실제로 구현한 exact Action metadata만 등록한다. 본 앱의 승인 범위는 10개 전부다.

### 2.3 스키마 공백

현재 Entity 스키마는 대부분 필드를 optional로 표현하며 문자열 날짜 형식, 길이, 검색 category 의미, 결과 수 제한, 오류 code를 규정하지 않는다. `Status`도 `Success`와 `Reason`만 제공한다. 따라서 provider 정책을 이 문서에서 명시하고, wire contract를 바꾸지 않는 범위에서 일관되게 검증해야 한다.

특히 생성 Action의 출력이 생성된 Entity가 아니라 `Status`뿐이므로, Action 호출자가 생성 ID를 회수할 수 없다. 이 앱은 다음 정책을 채택한다.

- UI 생성: 앱이 stable ID를 생성한다.
- Action 생성: 호출자가 유효한 `Entity.Id`를 반드시 제공한다.
- 이미 존재하는 ID로 동일 payload를 다시 생성하면 idempotent success, 다른 payload면 conflict failure다.
- 이 정책을 Action 사용 문서와 E2E fixture에 명시한다.

## 3. 목표, 비목표, 범위 trade-off

### 3.1 목표

- 예정 Reminder를 생성·조회·검색·수정·완료·삭제하는 end-to-end 제품 흐름
- viewing/recording Reservation 생성·목록·취소 흐름
- UI와 Agent 호출 간 동일한 상태 및 validation semantics
- 재시작 후 데이터 복원과 app-owned 알람/job reconciliation
- remote/D-pad, pointer, touch의 기능 동등성
- 현재 화면의 semantic Entity를 Agent가 이해할 수 있는 ViewAnnotation
- host, build, package, Common Emulator, 제품 target 증거를 서로 구분한 검증

### 3.2 비목표

- Galaxy Reminder의 계정 동기화, 위치 기반 알림, 공유, 음성 입력, 이미지/체크리스트 첨부
- 반복 Reminder(현재 Reminder Entity에 반복 규칙이 없음)
- snooze, 알림음/진동 상세 설정, OS 알림 센터 완전 통합
- platform `default-actions` 또는 Entity schema 변경
- Common Emulator에서 실제 tuner, EPG, 녹화 저장장치 동작을 가장하는 것
- Calendar event-linked reminder와의 암묵적 통합
- 삭제 취소용 장기 휴지통 또는 클라우드 복원

### 3.3 단계별 scope

| 단계 | 포함 | 제외/대체 | 이유 |
|---|---|---|---|
| P0 계약 기준선 | 10개 Schedule Action, Reminder/Reservation persistence, simulator, 기본 목록·상세·편집, ViewAnnotation | 실제 TV recording | Action 2.0 계약을 Common에서 재현 가능하게 검증 |
| P1 제품 target | TV capability adapter, EPG/recording integration | 계정 동기화 | 제품 권한·hardware가 있을 때만 검증 |
| 후속 후보 | 반복 Reminder, snooze, 분류 확장 | P0에 선반영 금지 | 기존 Entity/schema와 UX 복잡도를 늘리므로 별도 승인 필요 |

**Trade-off:** 10개 Action을 모두 지원하면서 Galaxy Reminder식 단순 IA를 유지하기 위해 홈은 Reminder 중심으로 두고, Reservation은 명시적인 두 번째 surface/tab으로 분리한다. 두 aggregate를 한 목록에 섞지 않는다.

## 4. 기능 요구사항과 acceptance criteria

### FR-01 홈 및 정보 구조

- 홈은 `예정` Reminder 목록을 기본 surface로 표시한다.
- `완료됨`은 별도 filter/surface이며 기본 목록을 압도하지 않는다.
- `예약` surface에서 viewing/recording을 함께 시간순으로 보여주되 kind를 텍스트와 아이콘으로 구분한다.
- 검색, 새 Reminder 추가, 예정/완료/예약 전환은 remote focus가 가능한 명시적 control이어야 한다.

**Acceptance criteria**

1. 비어 있는 최초 실행에서 설명 문구와 focus 가능한 `리마인더 추가` 동작이 보인다.
2. 시작 시 initial focus는 첫 예정 항목, 항목이 없으면 `리마인더 추가`에 간다.
3. 예정 목록은 `DueDate` 오름차순, 동률이면 생성 시각과 stable ID 순으로 deterministic 정렬한다.
4. 완료 목록은 완료 시각 내림차순으로 표시한다. 완료 시각은 앱 저장 모델에만 존재하고 Entity에는 노출하지 않는다.
5. Reservation은 `StartTime` 오름차순이며 recording/viewing kind가 색상만이 아닌 문구로 식별된다.

### FR-02 Reminder 생성

- 필수: 제목, due date/time
- 선택: note
- UI는 stable ID를 생성하고 Action 호출은 caller-supplied stable ID를 요구한다.
- due date는 offset을 포함한 RFC 3339 문자열로 입력받아 UTC instant로 정규화한다.

**Acceptance criteria**

1. 공백 제거 후 제목이 비면 저장되지 않고 제목 control에 오류와 focus가 유지된다.
2. 제목 1~200자, note 0~2,000자, ID 1~128자를 허용한다.
3. UI에서 과거 시각 생성은 막고 설명 오류를 표시한다. Action에서도 동일하게 `Success=false`를 반환한다.
4. 성공 시 JSON document가 atomic하게 저장되고, 미래·미완료 Reminder에는 정확히 하나의 app-owned alarm handle이 연결된다.
5. 저장 실패 또는 alarm 생성 실패 시 목록에 나타나지 않으며 새로 만든 외부 handle은 보상 취소된다.
6. 동일 ID·동일 payload 재호출은 중복 항목/알람 없이 success, 동일 ID·다른 payload는 conflict failure다.

### FR-03 Reminder 조회·검색

`SearchReminder(Tizen.Entity.Query)` 정책은 다음과 같다.

- `Keyword`: 제목과 note의 case-insensitive substring 검색. 비거나 생략하면 전체.
- `Category`: `upcoming`, `overdue`, `completed`, `all`; 비거나 생략하면 `upcoming`.
- `Number`: 최대 결과 수. 0 또는 생략은 50, 유효 범위 1~100.
- 정렬: upcoming/overdue는 due date 오름차순, completed는 완료 시각 내림차순, all은 미완료 우선 후 due date.

**Acceptance criteria**

1. UI 검색과 Action 검색이 같은 query service와 동일한 정렬/필터 의미를 사용한다.
2. `Number < 0` 또는 `Number > 100`, 알 수 없는 Category, 200자를 넘는 Keyword는 bounded failure다.
3. 결과가 없으면 `Success=true`, 빈 result를 반환한다.
4. 반환 Entity는 stable ID, Title, canonical RFC 3339 DueDate, Note, Completed를 포함한다.
5. 검색 결과 수는 항상 요청 limit 이하이며 입력/저장 순서와 무관하게 deterministic하다.

### FR-04 Reminder 수정

- `UpdateReminder`는 ID로 기존 항목을 식별하고 제공된 Entity 전체를 replacement payload로 해석한다.
- ID 변경은 허용하지 않는다.

**Acceptance criteria**

1. 존재하지 않는 ID는 not-found failure이며 새 항목을 만들지 않는다.
2. 제목, note, due date 검증은 생성과 동일하다.
3. due date 또는 completed 상태가 바뀌면 alarm을 정확히 reschedule/cancel한다.
4. 새 alarm 준비 또는 persistence가 실패하면 이전 데이터와 이전 alarm이 유지된다.
5. 성공 후 UI와 `SearchReminder`에서 즉시 동일 snapshot이 보이고, 재시작 후에도 유지된다.

### FR-05 Reminder 완료

- `CompleteReminder`는 input의 ID만 identity로 사용하여 미완료 항목을 완료 상태로 전이한다.
- 이미 완료된 항목에 대한 호출은 idempotent success다.

**Acceptance criteria**

1. 완료 후 `Completed=true`, 완료 시각이 앱 모델에 기록되고 예정 목록에서 제거된다.
2. 연결된 alarm은 취소되며 앱 저장 상태에는 active alarm handle이 남지 않는다.
3. 존재하지 않는 ID는 not-found failure다.
4. 완료 항목을 미완료로 되돌리는 기능은 P0 UI/Action 범위 밖이다. `UpdateReminder`로 암묵적 reopen을 허용하지 않는다.

### FR-06 Reminder 삭제

- `DeleteReminder`는 input의 ID로 삭제한다.
- UI 삭제는 확인 modal을 거치며 Action 호출은 명시적 호출 자체를 확인으로 간주한다.

**Acceptance criteria**

1. UI modal은 focus를 trap하고 `취소`를 기본 focus로 한다.
2. 삭제 성공 후 저장소, 목록, 검색, ViewAnnotation에서 항목이 사라지고 alarm이 취소된다.
3. 존재하지 않는 ID 삭제는 안전한 idempotent success로 처리하되 `Reason`에 already absent를 나타낸다.
4. persistence 실패 시 데이터/화면/alarm을 이전 상태로 유지한다.

### FR-07 Reservation 생성

- `AddViewing`과 `AddRecording`은 각각 `Reservation.Kind`를 결정한다.
- 필수: stable ID, StartTime, EndTime, Channel 또는 Program을 통한 식별 가능한 방송 정보
- `Repeat`: `once`, `daily`, `weekly`, `weekdays`; 생략은 `once`

**Acceptance criteria**

1. `EndTime > StartTime`, ID 1~128자, offset 포함 RFC 3339를 검증한다.
2. Action 이름과 payload Kind가 충돌하면 failure다. Kind 생략 시 Action 이름으로 정규화한다.
3. Program이 있으면 stable Program ID를 요구한다. Program이 없으면 stable Channel ID와 표시 가능한 채널 정보를 요구한다.
4. 동일 ID·동일 payload 재호출은 idempotent success, 다른 payload는 conflict failure다.
5. Common Emulator adapter는 app 내부 simulator job만 만들고 화면에 `시뮬레이션`임을 표시한다.
6. 실제 recording 성공은 TV profile, 권한, tuner/EPG/storage capability를 검증한 제품 adapter에서만 주장한다.

### FR-08 Reservation 조회 및 취소

- `GetReservations`는 현재 시각 이후의 active Reservation을 반환한다.
- `CancelViewing`/`CancelRecording`은 ID로 식별하고 kind 일치를 검증한다.

**Acceptance criteria**

1. 결과는 StartTime 오름차순이며 stable ID를 포함한다.
2. cancel Action과 저장된 kind가 다르면 failure이며 다른 kind의 job을 취소하지 않는다.
3. 취소 성공 후 저장소, UI, `GetReservations`, ViewAnnotation에서 사라지고 app-owned job handle이 제거된다.
4. 이미 취소/부재인 ID는 idempotent success, 다른 앱/시스템 소유 예약은 절대 변경하지 않는다.
5. 반복 예약은 다음 발생 시각 계산을 timezone/DST 테스트로 검증한다.

### FR-09 알람 발생과 lifecycle

- 미래의 미완료 Reminder에는 하나의 notification/alarm이 대응한다.
- viewing Reservation은 시작 시각 전 알림 또는 simulator trigger를 갖는다.
- recording Reservation은 제품 adapter의 예약 job 또는 Common simulator job을 갖는다.

**Acceptance criteria**

1. 알람 발생 후 Reminder 데이터는 자동 완료/삭제되지 않는다.
2. 앱이 종료되어도 플랫폼 alarm이 가능한 환경에서는 알림이 동작한다.
3. due time이 지난 미완료 항목은 overdue로 유지하며 재시작 때 과거 alarm을 다시 등록하지 않는다.
4. 앱 시작 시 persisted desired state와 app-owned handle을 reconcile하여 누락 handle은 재생성하고 orphan handle은 제거한다.
5. 앱은 명시적인 ownership prefix/namespace가 없는 외부 alarm/job을 조회·취소하지 않는다.

### FR-10 입력 방식과 navigation

**Remote/D-pad 기본 계약**

- Up/Down: 같은 영역의 이전/다음 항목
- Left/Right: 상단 surface/filter 또는 항목 내부 명시 action 간 이동
- Enter: focused 항목 열기 또는 focused control 실행
- Back: modal 닫기 → editor/detail 닫기 → search 해제 → root에서 앱 종료

**Acceptance criteria**

1. remote, pointer, touch가 같은 semantic command/reducer 경로를 호출한다.
2. pointer/touch는 Down 후 Up-inside일 때 한 번만 활성화하며 Leave/Interrupted에서 취소한다.
3. Add, Search, Save, Cancel, Complete, Delete, 예약 취소가 모두 D-pad로 도달 가능하다.
4. 저장 성공 후 해당 목록 항목, 취소/검증 실패 후 원래 control, 삭제 후 인접 항목 또는 empty-state CTA로 focus가 복원된다.
5. modal 밖으로 focus가 빠져나가지 않고 dead-end/cycle 오류가 없다.

## 5. 비기능 요구사항

| ID | 품질 속성 | 요구사항 | 검증 기준 |
|---|---|---|---|
| NFR-01 | 신뢰성 | persistence와 외부 alarm/job 사이 partial failure가 사용자 상태를 손상시키지 않아야 한다. | fault injection으로 schedule/persist/cancel 각 단계 실패 후 이전 또는 새 상태 중 하나만 관찰됨 |
| NFR-02 | 일관성 | UI와 10개 Action이 하나의 serialized mutation boundary를 공유한다. | UI/Action 동시 mutation 테스트에서 duplicate ID, lost update, duplicate alarm이 없음 |
| NFR-03 | 복구성 | 비정상 종료·재시작 후 atomic document와 app-owned handle을 reconcile한다. | temp/backup/corrupt document 및 orphan/missing handle fixture로 deterministic 복구 |
| NFR-04 | 성능 | 1,000 Reminder + 500 Reservation 기준 warm search p95 200 ms 이하, mutation service p95 500 ms 이하(플랫폼 notification UI 시간 제외) | host benchmark와 Emulator trace를 별도 기록 |
| NFR-05 | 자원 제한 | Search 100개, ID/문자열 길이, persisted collection 10,000개를 상한으로 둔다. | 경계값/초과 입력이 명시 failure이며 메모리 폭증 없음 |
| NFR-06 | 사용성 | 상태·kind·focus·오류를 색상 하나에 의존하지 않는다. | light/dark, 200% text scale, remote focus visual review |
| NFR-07 | 접근성 | control마다 의미 있는 label/role/state가 있고 focus indicator 대비가 명확해야 한다. | NUI accessibility tree와 실제 focus traversal 캡처 |
| NFR-08 | 개인정보 | annotation/log/status Reason에 불필요한 note나 전체 저장 document를 노출하지 않는다. | log scan, annotation field audit; 상세 note는 현재 화면에 실제 표시될 때만 게시 |
| NFR-09 | 유지보수성 | Domain/UseCases는 Tizen assembly 없이 host 실행 가능하며 generated adapter는 thin하다. | 프로젝트 dependency 검사와 host tests |
| NFR-10 | 호환성 | 전체 Schedule category generation과 기존 method ID 2..11을 보존한다. | baseline/current generated MethodId 비교 및 byte provenance 검사 |
| NFR-11 | 관찰성 | mutation/reconciliation에는 operation, entity kind, hashed/truncated ID, outcome, compensation 여부를 기록한다. | 민감 내용 없이 correlation 가능한 structured log 검사 |
| NFR-12 | 이식성 | Common simulator 결과와 TV 제품 결과를 분리한다. | 테스트 보고서에 `Common-simulated`/`TV-product` evidence label이 존재 |

## 6. Action / Entity / ViewAnnotation 경계

### 6.1 책임 구조

```text
NUI Views / Schedule Action Provider / Internal View Provider / Platform Adapters
                               |
                               v
              ReminderCommandService + ScheduleQueryService
                               |
                               v
        Reminder / Reservation Domain + Repository interfaces
                               |
                               v
      Atomic Persistence / Alarm / Reservation Job implementations
```

- Domain: invariant, state transition, stable ID, 정렬/검색 규칙
- Use cases: transaction orchestration, concurrency serialization, compensation, reconciliation
- NUI: render, semantic UI command, focus/navigation, validation 표현
- Schedule provider: generated DTO validation/conversion, use-case 호출, Status mapping
- View provider: 현재 화면 snapshot, actual geometry/focus, generated Entity serialization
- Platform adapters: file I/O, notification/alarm, TV recording/viewing 또는 Common simulator

### 6.2 Action 경계

- 외부 앱/Agent가 호출할 cross-app capability만 Schedule Action이다.
- `탭 전환`, `목록 focus 이동`, `검색창 열기`, `detail 열기`, `Back`, `완료 확인 modal`은 local UI state이며 새 public Action으로 만들지 않는다.
- generated `ServiceBase`는 10개 전체 method를 compile하고 앱은 10개 전부 advertise한다.
- provider는 `Status.Reason`을 안정적인 prefix로 시작한다: `invalid:`, `not-found:`, `conflict:`, `unavailable:`, `internal:`. 이는 새 wire 필드를 추가하지 않고 테스트 가능한 오류 범주를 제공한다.
- schema의 `details.appid=com.samsung.tv.reminder`와 예제 appid가 다르므로 provider discovery와 explicit appid routing을 target에서 검증한다. compile 성공으로 routing을 주장하지 않는다.

### 6.3 Entity 경계

- `Tizen.Entity.Reminder` 및 `Reservation`은 wire DTO이며 domain object가 아니다.
- 앱 저장 모델은 `CreatedAt`, `UpdatedAt`, `CompletedAt`, timezone metadata, alarm/job handle, simulator/product mode, revision을 추가로 가질 수 있다.
- `Extra`에 앱 private persistence나 민감 metadata를 넣지 않는다. wire 확장이 꼭 필요해질 때는 별도 schema 변경 승인을 받는다.
- stable `Id`는 생성 후 변하지 않는다.
- Reminder에 entity resolver metadata/GetById Action이 없으므로 ViewAnnotation identity refresh는 `SearchReminder`만으로 정확한 단건 조회를 보장할 수 없다. P0에서는 Annotation의 snapshot을 authoritative publication-time context로 사용하고, 새 resolver를 암묵적으로 발명하지 않는다. resolver가 제품 요구가 되면 platform schema 변경을 별도 제안한다.

### 6.4 ViewAnnotation 경계

게시 대상:

- 현재 viewport에 실제 렌더된 Reminder list row
- Reminder detail의 주 Entity card
- 현재 viewport에 실제 렌더된 Reservation row/detail card

게시하지 않는 대상:

- Add/Search/Back/Save 같은 일반 control
- 화면 밖 virtualized row
- editor의 저장 전 draft
- modal 배경 또는 stale 이전 surface

계약:

1. View ID는 `reminder:item:<EntityId>` 또는 `reminder:reservation:<EntityId>` 형식이다.
2. `Annotation.EntityType`은 각각 `Tizen.Entity.Reminder`, `Tizen.Entity.Reservation`이다.
3. `Annotation.EntityId`는 stable Entity ID다.
4. `EntityJson`은 해당 generated DTO의 `ToJson()` 결과다. 병렬 JSON serializer를 만들지 않는다.
5. 목록 row annotation에는 화면에 실제 보이는 정보만 DTO에 매핑한다. note가 화면에 보이지 않으면 비워 개인정보 노출을 최소화한다. detail에서 note가 보일 때만 포함한다.
6. bounds는 `CalculateScreenPositionSize()` 등 실제 NUI screen-space 값이며 finite이고 width/height가 양수일 때만 게시한다. synthetic zero 좌표는 사용하지 않는다.
7. focus는 `FocusManager`의 actual focused View와 active surface subtree를 기준으로 한다.
8. render/layout/focus 변경 시 snapshot을 교체하고 pause/background/terminate 시 clear한다. resume 후 fresh bounds를 다시 측정한다.
9. internal View Actions 4개를 advertise한다면 `FindById`, `GetAnnotatedViews`, `GetFocusedView`, `ToPresentation`을 모두 성공 가능한 실제 구현으로 제공한다. unsupported placeholder를 advertise하지 않는다.
10. `ToPresentation`은 Annotation의 generated Entity JSON에서 A2UI `surfaceUpdate` Template과 matching `dataModelUpdate` Document를 각각 생성한다.

## 7. 데이터 및 알람 lifecycle

### 7.1 상태 모델

Reminder:

```text
Draft(UI only) -> Scheduled -> Overdue -> Completed
                       \-----------------> Deleted
```

- `Overdue`는 저장 enum이 아니라 `!Completed && DueAt < now`에서 계산 가능하다.
- `Completed`와 `Deleted`는 별개다. 완료 데이터는 보존하고 삭제 데이터는 P0에서 제거한다.
- draft는 persistence, Action, annotation에 노출하지 않는다.

Reservation:

```text
Draft(UI only) -> Active -> Triggered/Elapsed
                       \-> Cancelled
```

- P0 저장 최적화로 cancelled/elapsed 항목을 즉시 물리 삭제할 수 있지만, active job handle cleanup 완료 여부는 reconciliation journal 또는 tombstone으로 추적해야 한다.

### 7.2 mutation 순서

Create/Add:

1. 입력 검증 및 ID conflict 확인
2. 현재 document/repository snapshot 확보
3. 새 app-owned alarm/job 준비
4. desired document를 temp file + flush + atomic replace로 저장
5. in-memory repository snapshot 교체
6. 실패 시 이번 작업에서 새로 만든 handle만 취소

Update/reschedule:

1. 기존 state/handle snapshot 확보
2. replacement alarm 준비
3. 새 desired document atomic 저장
4. in-memory snapshot 교체
5. 이전 handle 취소
6. 이전 handle 취소 실패는 cleanup journal에 기록하고 startup/periodic reconciliation에서 재시도

Complete/Delete/Cancel:

1. desired state 또는 제거 + cleanup intent를 atomic 저장
2. in-memory snapshot 교체
3. 이전 app-owned handle 취소
4. 취소 실패 시 ownership 정보가 든 cleanup intent를 유지해 재시도

이 순서는 persistence 성공 전 화면에 mutation을 publish하지 않고, 외부 side effect의 orphan을 추적 가능하게 한다.

### 7.3 startup reconciliation

1. primary document 검증; 실패하면 backup recovery 시도
2. schema version migration 및 invariant 검증
3. 유효한 domain snapshot 구성
4. app-owned alarm/job 목록 조회
5. 미래·미완료 desired 항목의 missing handle 생성
6. 완료/삭제/취소/과거 항목의 orphan handle 제거
7. handle 변경이 있으면 document atomic 갱신
8. reconciliation 완료 후 provider mutation과 UI 편집을 허용

복구 중 UI/Action은 loading 또는 `unavailable: reconciliation` 상태를 반환하고 반쯤 복구된 snapshot을 노출하지 않는다.

### 7.4 시간 정책

- wire input: offset 포함 RFC 3339만 허용 (`Z` 포함). timezone 없는 local string은 reject한다.
- persistence: UTC instant + 원래 offset/timezone hint(앱 private metadata)
- display: 현재 시스템 locale/timezone
- repeat Reservation: local wall-clock 의도를 보존해야 하므로 DST gap/overlap 정책을 명시한다. gap은 다음 유효 시각, overlap은 더 이른 offset을 기본으로 하며 테스트로 고정한다.
- 시스템 시각/timezone 변경 후 reconciliation을 수행한다.

## 8. 검증 전략과 주요 위험

### 8.1 검증 계층

| 계층 | 증명하는 것 | 증명하지 못하는 것 |
|---|---|---|
| Host domain/use-case | invariant, query semantics, transaction order, compensation, concurrency | Tizen RPC/runtime, 실제 NUI focus |
| Generated compile/provenance | 전체 category signature, method ID, source 재현성 | provider discovery와 실행 |
| Package inspection | manifest metadata, assembly/payload | 실제 target routing |
| Common Emulator Action E2E | discovery, explicit appid routing, wire DTO/status, simulator postcondition | 실제 TV tuner/recording |
| Common Emulator UI | NUI layout, remote/pointer/touch, bounds/focus annotation | TV 제품 UX/capability |
| TV product gate | tuner/EPG/storage/권한과 실제 viewing/recording | 다른 제품군 일반화 |

### 8.2 Action acceptance matrix

각 Action은 최소 positive 1개와 bounded negative 1개를 실제 RPC로 실행하며 mutation 후 query postcondition을 확인한다.

| Action | Positive | Negative | Postcondition |
|---|---|---|---|
| AddRecording | future valid recording | end <= start 또는 capability unavailable | GetReservations에 동일 ID/kind |
| AddViewing | future valid viewing | kind conflict | GetReservations에 동일 ID/kind |
| CancelRecording | existing recording | viewing ID로 cancel | 대상만 제거, viewing 유지 |
| CancelViewing | existing viewing | recording ID로 cancel | 대상만 제거, recording 유지 |
| CompleteReminder | active reminder | unknown ID | completed search에 ID, alarm 없음 |
| CreateReminder | valid caller ID | blank title/duplicate conflict | upcoming search와 restart 복원 |
| DeleteReminder | existing ID | malformed/oversized ID | 검색에서 제거, alarm 없음 |
| GetReservations | mixed active list | 저장소/reconciliation unavailable | 정렬·kind·stable ID 확인 |
| SearchReminder | keyword/category/limit | invalid category/limit | UI 결과와 parity |
| UpdateReminder | due date 변경 | unknown ID/invalid date | 새 값, alarm 1개, restart 복원 |

### 8.3 위험 register

| 우선순위 | 위험 | 영향 | 완화 및 release gate |
|---|---|---|---|
| Critical | 생성 결과에 Entity ID가 없음 | Agent가 생성 항목을 재식별하지 못함 | caller-supplied ID 필수 + idempotency E2E; 향후 schema 개선은 별도 ADR |
| Critical | 저장 성공/알람 실패 또는 반대 partial failure | 유실 알림, ghost alarm | staged mutation, cleanup intent, fault-injection 및 restart reconciliation |
| Critical | subset TIDL 생성으로 method ID 변경 | 런타임 ABI 불일치 | whole-category generation, action.seq baseline ID 2..11 비교 |
| High | schema 필드 optional/날짜 format 미정 | provider별 의미 분기 | 본 문서의 validation/default 정책과 wire E2E 고정 |
| High | schema appid와 sample appid 불일치 | provider compile 후 실제 routing 실패 | Action DB discovery + explicit sample appid invocation |
| High | Common에서 녹화를 성공으로 과장 | 잘못된 제품 주장 | simulator UI/로그 표시, 제품 gate 별도, capability status |
| High | cancel 후 handle ownership 정보 유실 | 다른 자원 취소 또는 orphan | namespaced ownership + cleanup journal; app-owned handle만 조작 |
| High | UI와 provider가 별도 repository/service 사용 | stale UI, lost mutation | composition root에서 같은 singleton service 주입, concurrency test |
| High | ViewAnnotation stale bounds/focus | Agent가 잘못된 UI를 조작 | active surface actual geometry/focus, pause clear, layout republish E2E |
| Medium | note가 annotation/log에 노출 | 개인정보 유출 | visible-field projection 후 generated ToJson, structured log redaction |
| Medium | Search Query 의미가 범용 schema라 모호 | Agent 결과 예측 불가 | Category/Number semantics 문서화 및 UI/Action parity test |
| Medium | 반복 Reservation의 DST 오류 | 잘못된 시각에 실행 | timezone-aware recurrence tests; Reminder repeat는 P0 제외 |
| Medium | Status에 structured code 없음 | 소비자 error handling 취약 | Reason prefix 안정화, 문서/contract test; schema 변경은 별도 승인 |
| Medium | NUI input mode별 별도 handler | 중복 실행/기능 불일치 | semantic command 단일 경로, Down/Up-inside pointer 규칙 |

## 9. 구현 승인 전 확인할 open questions

다음은 구현 전에 제품/플랫폼 책임자가 확정해야 한다. 확정되지 않으면 아래 권장 기본값을 사용한다.

1. **예제 appid:** 기존 `com.samsung.tv.reminder`를 재사용하지 않고 저장소 namespace의 별도 appid를 사용한다.
2. **Reminder 재열기:** P0에서는 완료 취소를 제공하지 않는다.
3. **과거 due 생성:** P0에서는 UI와 Action 모두 reject한다.
4. **Reservation UI 생성:** 10개 Action 계약 검증을 위해 P0에 포함하되, 방송 정보는 deterministic fixture/catalog에서 선택한다.
5. **Common recording:** simulator success로만 기록하고 UI에 `시뮬레이션` 배지를 표시한다.
6. **View Annotation note:** 목록에서는 제외, 상세 화면에 실제 표시될 때만 포함한다.
7. **삭제 audit/tombstone:** 사용자 데이터 tombstone은 P0 제외하되 외부 handle cleanup intent는 완료될 때까지 유지한다.

## 10. Definition of Ready / Done

### 구현 시작 가능(Ready) — Approved B, 2026-08-09

- [x] 10개 Action의 입력 default, validation, idempotency 정책 승인
- [x] caller-supplied ID 정책 승인
- [x] Reminder/Reservation 분리 IA와 B mockup 승인
- [x] Common simulator와 TV product adapter 경계 승인
- [x] persistence/alarm cleanup intent 전략 승인
- [x] ViewAnnotation 공개 필드 및 note privacy 정책 승인

### 완료(Done)

- [ ] Tizen-free domain/use-case/persistence tests 통과
- [ ] 전체 Schedule category generated source 재현 및 기존 MethodId 보존
- [ ] 10개 exact provider metadata와 sample appid discovery 확인
- [ ] 10개 Action positive/negative RPC 및 query postcondition 통과
- [ ] create/update/complete/delete/cancel의 restart 및 alarm/job compensation 통과
- [ ] remote/D-pad, pointer, touch parity와 Back/focus restoration 통과
- [ ] actual bounds/focus, pause/resume, annotation generated JSON, A2UI E2E 통과
- [ ] Common simulator 결과와 TV product 미검증 항목을 분리 보고
- [ ] generated source 수동 수정이 없고 `git diff --check` 통과
