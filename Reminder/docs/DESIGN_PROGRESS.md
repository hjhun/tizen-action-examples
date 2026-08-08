# Reminder 설계 진행 기록

- 시작: 2026-08-09 00:17 KST
- 현재 단계: Approved B 구현 및 Common Emulator 검증 완료
- 구현 상태: Option B Focused Workspace, provider/core, package/install, GUI Automation, Schedule/View E2E 완료

## 목표

Samsung Reminder의 정보구조와 interaction model을 참고하여 Tizen Action Framework 2.0 예제 Reminder 앱의 기능·비기능 요구사항과 HTML UI 방향을 확정한다.

## 완료한 조사

1. 저장소의 `AGENTS.md`, Calendar 설계·개발 문서, domain app catalog를 검토했다.
2. `Tizen.Action.Schedule`의 `action.seq` 순서와 10개 Action 계약을 확인했다.
3. `Tizen.Entity.Reminder`, `Tizen.Entity.Reservation`, `Tizen.Entity.Query` schema를 확인했다.
4. 기존 Graphify store를 조회했다. 현재 graph는 CMake/package 구조만 추출하며 `.action`/`.entity` 의미 관계를 포함하지 않아, Schedule 계약은 원본 schema에서 직접 확인했다.
5. Samsung 공식 페이지/Galaxy Store 접근을 시도했으나 일부 URL은 redirect, 지역 제한 또는 404였다. 따라서 확인 가능한 제품 동작과 제3자 One UI 8 walkthrough 관찰을 구분하여 사용한다.
6. One UI 8 walkthrough에서 관찰한 패턴: 홈 화면의 Today/Scheduled/Important/Place/No alert/Completed 스마트 분류, 접기 가능한 category 영역, 단일 화면 중심 생성, date/time/repeat/location/checklist/category/attachment 도구의 하단 집중, 검색과 overflow 메뉴.
7. Architect, Product/UI Designer, CX·Accessibility 전문가 관점의 독립 검토를 병렬 의뢰했다.

## HTML reference

- `../refs/reminder-design-options.html`
- A — Galaxy Hub: 스마트 목록과 category를 홈 상단에 노출
- B — Focused Workspace: 좌측 스마트 목록 + 중앙 목록 + 우측 상세 (현재 권장)
- C — Timeline Board: Today/Tomorrow/Later 칼럼

## 현재 권장 가설

B를 기본 shell로 사용하고 A의 스마트 분류를 중앙 상단의 접을 수 있는 filter chip으로 통합한다. Galaxy Reminder의 빠른 분류 접근성을 살리면서 TV remote의 결정적 좌→우 focus 이동과 상세 확인을 단순화할 수 있다.

## 확정 전 쟁점

- Reminder 전용 사용자 흐름과 TV viewing/recording Reservation 흐름을 같은 최상위 UI에 둘지, 별도 `Reservations` 영역으로 분리할지
- Reminder schema에 없는 category, priority, checklist, location, attachment를 MVP domain extension으로 둘지 또는 HTML에서 후속 범위로만 표현할지
- 알림 없는 task, 반복 알림, snooze를 MVP에 포함할지
- 기준 viewport와 Public Common Emulator에서 사용할 input 조합

## 전문가 검토 통합

### Architect

- 검토 원문: `REQUIREMENTS_ARCHITECTURE_REVIEW.md`
- Reminder와 Reservation을 별도 aggregate로 유지하되 하나의 product shell에서 제공한다.
- 생성 Action 결과가 Status뿐이라는 중요한 계약 공백을 발견했다. 따라서 Action 생성은 caller-supplied stable ID와 idempotency/conflict 정책이 필요하다.
- Status가 `Success`/`Reason`만 제공하므로 stable reason prefix를 contract로 고정해야 한다.
- app-owned alarm/job은 파생 자원으로 보고 atomic persistence, cleanup intent, startup reconciliation이 필요하다.
- Common Emulator simulator와 TV product adapter 검증을 분리한다.

### Product/UI Designer

- 1920×1080에서는 adaptive 3-pane master-detail이 가장 적합하다.
- Today를 기본 진입점으로 하고, 제목·기한을 primary 정보로, 반복·priority·category는 secondary로 둔다.
- dashboard형 layout은 시각적 인상은 좋지만 D-pad 이동 비용과 빈 화면 위험 때문에 기본 IA로 부적합하다.
- 작은 viewport에서는 같은 IA를 2-pane 또는 단일 화면으로 축소해야 한다.

### CX·Accessibility

- 핵심 경험을 `빠른 생성 → 예정/기한 초과 확인 → 완료 → 즉시 Undo`로 정의한다.
- 목록 재정렬, modal, 외부 Action mutation 뒤 focus 유실을 P0 위험으로 다룬다.
- pointer/touch target 44×44 이상, TV 주요 control 60×60 목표, 색상 외 2개 이상의 focus cue가 필요하다.
- 알람 생성 실패와 reminder 저장 실패를 구분하고 silent degradation을 금지한다.

### 초안에 반영한 항목

- caller-supplied ID, idempotency/conflict, stable Reason prefix
- replacement alarm 준비 → atomic persistence → publish → old handle cleanup 순서
- 제한된 UI Undo와 일반 Restore/Reopen P0 제외
- 목록 annotation note 최소화와 detail-visible disclosure
- 44×44/60×60 hit target 요구사항
- No alert/due 필수 정책은 전문가 의견이 갈려 승인 쟁점으로 유지

## 작성·검증 상태

- 기능·비기능 요구사항 초안: `REQUIREMENTS_DRAFT.md`
- 요구사항 규모: 기능 요구사항 58개, 비기능 요구사항 30개
- 미완성 placeholder 검사: 없음
- Markdown 상대 HTML link 존재 확인: 통과
- HTML 실제 브라우저 렌더 확인: 통과
- A/B/C 탭 전환과 1280px viewport horizontal overflow 검사: 통과
- 기본 B 화면에서 navigation/list/detail/focus visual cue와 component 겹침 없음 확인
- `git diff --check`: 통과

## 승인 및 구현 결과

- 2026-08-09 사용자가 B Focused Workspace를 승인했다.
- 구현은 좌측 smart navigation, 중앙 bounded list, 우측 detail/editor의 3-pane shell을 사용한다.
- 앱 ID는 `org.tizen.actionexamples.reminder`다.
- Tizen-free Domain/Persistence/UseCases와 host tests, 10개 Schedule method provider, 현재 View contract provider, Common deterministic simulator를 구현했다.
- Tizen 10.1 Common Emulator(`emulator-26101`)에 최신 TPK를 설치하고 Schedule/View provider discovery를 확인했다.
- Schedule 10개 Action의 실제 wire path, mutation postcondition, restart persistence가 통과했다.
- 1920×1080 native NUI에서 B 3-pane, contrast, card/detail/editor, D-pad rail→filter→list→detail focus route를 Aurum으로 검증했다.
- Today/Upcoming/Overdue/Completed/All/Reservations 6개 page와 reminder detail, reservation detail, No-alert filter, new editor를 final TPK에서 캡처했다.
- 실제 `Window.Default.WindowSize`와 `GetInsets()`로 1920×1080 design canvas의 uniform scale과 centered X/Y offset을 계산하고 runtime resize/inset 변경에서 re-render하도록 구현했다.
- viewport 계산은 1920×1080, 1280×720, 1440×1080(4:3), 2560×1080(ultrawide) host test를 통과했다.
- View 4개 Action에서 list/detail 두 annotation, 실제 focused card, positive `ScreenBounds`/`WindowBounds`, generated `EntityInfo`, `FindById`, A2UI `ToPresentation`을 확인했다.
- device E2E 중 Reservation의 nested Program.Channel 누락과 View 실패 output의 nullable geometry serializer crash를 발견해 app-owned adapter에서 수정했다.
- 절차: [`BUILD_E2E_GUIDE.md`](BUILD_E2E_GUIDE.md)

## 후속 제품 gate

1. 1280×720 native Emulator/device에서 proportional 3-pane, focus target, text readability, View Annotation bounds를 별도로 검증한다.
2. 실제 TV tuner/recording adapter와 TV profile 검증은 Common simulator와 분리해 수행한다.
