# Reminder 페이지별 ViewAnnotation

[English](VIEW_ANNOTATION_Eng.md)

2026-09-06 최종 구현은 API 14와 표준 Reminder 5개/ReminderCustom 6개/View 4개 Action을 사용합니다.

검색어는 입력 중인 draft와 Search로 적용한 keyword를 분리합니다. 타이핑 중에는
기존 목록과 그 Entity 주석을 유지하고, 입력 컨트롤에는 현재 draft를 게시합니다.
목록 주석은 필터를 재실행하지 않고 실제 렌더한 첫 여섯 항목의 ID를 사용합니다.
검색어를 지운 후에도 Search를 눌러야 필터가 해제됩니다.

[최종 API 14 검증](../../Calendar/docs/STAGE1_VALIDATION.md)에 현재 catalog, 해상도, View 및 Presentation 결과를 기록했습니다.

| 화면 | 공개 정보 |
|---|---|
| Today / Upcoming / Overdue / Completed / All | section, list/detail/new/edit mode, 적용 keyword·timeFilter·selectedId, 실제 렌더된 Reminder |
| 빈 목록 / 빈 검색 | page context와 검색·필터·추가 컨트롤 |
| Reminder detail | 목록과 별도 detail View, 현재 표시된 note, Complete/Edit/Delete |
| Reminder 신규 / 수정 editor | 계속 보이는 목록, live title/due/note 컨트롤과 Save/Cancel. 교체된 detail은 제외 |
| Reservations 목록 / 상세 | 실제 렌더된 Reservation·선택 detail·취소 컨트롤. 기존 app-owned simulator 동작 유지 |

페이지 ID는 `reminder:page:<section>:<list|detail|new|edit>`입니다. 같은 페이지에서 검색이나 시간 필터를 바꾸면 ID는 유지하고 EntityInfo가 달라집니다. 이전 page의 ID는 `FindById`에서 제거됩니다.

항목은 generated `TizenEntityReminder`/`TizenEntityReservation`의 `ToJson()`을 사용합니다. 페이지·컨트롤은 generated `TizenEntity`이며 Extra에 `{schemaVersion:1,page,draft?}`를 저장합니다. `draft`에는 실제 control key/text/selected 상태가 들어갑니다. 저장된 Entity와 저장 전 입력을 구분합니다. 목록 note는 비우고 현재 detail에서 표시한 note만 포함합니다.

`GetFocusedView`는 현재 `GetAnnotatedViews`에 포함된 실제 NUI focused View를 반환합니다. 터치·포인터 편집 시 TextField/TextEditor의 KeyInputFocus를 우선합니다. 신규/수정 editor 진입 시 title field에 초기 focus를 줍니다. 실제 annotated focus가 없으면 실패를 반환하고 page나 첫 항목으로 대신하지 않습니다.

root 교체·pause/terminate 시 이전 snapshot을 비웁니다. focus/input/layout 변경은 현재 측정 가능한 tree를 즉시 원자적으로 게시하고 50ms 타이머와 relayout에서 다음 frame을 다시 측정합니다. resume는 최신 화면을 게시합니다. ScreenBounds는 `CalculateScreenPositionSize()`에서 얻은 finite positive 값이며 WindowBounds는 window origin을 뺀 값입니다. 이전 `View.Size` fallback을 제거했습니다. hidden/detached/미측정 view는 게시하지 않습니다.

`View_ToPresentation`은 Reminder/Reservation/page/control의 현재 snapshot 필드를 Text/Column에 바인딩하는 legacy v0.8 profile입니다. 잘못된 타입·EntityId·JSON·과도한 크기는 실패합니다. canonical v0.9.1 지원 주장은 아닙니다. 실제 legacy profile 왕복은 타깃에서 검증했습니다.

Host에서는 두 Reminder suite와 Calendar.App.Tests의 공통 page/store 테스트가 통과했습니다. Reminder Release build도 통과했습니다. 실제 target RPC·native focus/bounds·Presentation 표시는 [1단계 기록](../../Calendar/docs/STAGE1_VALIDATION.md)에 검증했습니다. [Calendar의 target 검증 절차](../../Calendar/docs/VIEW_ANNOTATION.md)를 위 표의 모든 Reminder 화면에 적용하고, 기존 screenshot을 이번 변경의 증거로 사용하지 마십시오.

## Authoritative live-view resolution (API 14)

`View_ToPresentation` resolves the supplied View ID, EntityType and EntityId against the atomically published current store. Caller-supplied EntityInfo is not authoritative. Hidden/removed/mismatched identities fail; stale or forged content for a valid visible identity is replaced with current data. Twenty consecutive GetAnnotatedViews/FindById pairs and actual display round trips passed on target; see the stage 1 evidence record.
