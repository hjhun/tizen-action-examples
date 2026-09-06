# Calendar 페이지별 ViewAnnotation

[English](VIEW_ANNOTATION_Eng.md) · [변경 기록](2026-09-06-dotnet-update.md)

## 현재 화면 계약

`GetAnnotatedViews`는 현재 active surface의 페이지, 실제 렌더 항목, 포커스 가능한 컨트롤을 반환합니다. `GetFocusedView`는 같은 목록의 실제 focused View를 반환하며, `FindById`는 현재 목록에서만 찾습니다. 조회 결과를 캐시한 consumer는 화면 전환 후 다시 조회해야 합니다.

| Calendar 화면 | 공개 정보 |
|---|---|
| Month / Week / Day / Agenda | mode·선택 날짜·표시 월, 렌더된 일정, 날짜/명령 컨트롤 |
| Search 초기 / 결과 / 빈 결과 | 검색 조건·적용 여부·결과 수, 입력/selector, 실제 렌더된 결과 |
| EventDetail | 선택 Event ID, 일정 snapshot과 Edit/Delete/Close |
| EventEditor 신규 / 수정 | 별도 page ID, 저장 대상 ID, 현재 입력 필드 값과 Save/Cancel |
| DeleteEventConfirmation | 삭제 대상 일정, 확인/취소 컨트롤 |
| ReminderList / 빈 목록 | 페이지 context, 실제 렌더된 독립 Reminder, Add/Done/Reopen |
| ReminderEditor 신규 / 수정 | 별도 page ID, 대상 ID, 현재 입력 필드와 명령 |
| DeleteReminderConfirmation | 삭제 대상 Reminder와 확인/취소 |

빈 화면에도 측정된 page annotation이 있습니다. Calendar overlay가 열리면 가려진 Month/Week/Day/Agenda는 게시하지 않습니다. Search context는 결과 수만 포함하며 화면 밖 result ID 목록을 노출하지 않습니다.

## Entity와 View ID

- 일정: `Tizen.Entity.CalendarEvent`, generated `TizenEntityCalendarEvent.ToJson()`.
- 리마인더: `Tizen.Entity.Reminder`, generated `TizenEntityReminder.ToJson()`.
- 페이지/컨트롤: `Tizen.Entity`, generated `TizenEntity.ToJson()`. `Extra`는 `schemaVersion: 1`, `page`, 선택적으로 `draft`를 가진 앱 context JSON입니다.
- 저장 전 텍스트는 `draft.control`, `draft.text`, 선택 컨트롤의 `draft.selected`에 반영됩니다. 입력 변경 후 다시 게시합니다.
- 목록의 note는 생략합니다. 현재 detail에서 보여 주는 note만 domain snapshot에 포함합니다.

페이지 ID 예: `calendar:page:Month`, `calendar:page:Search`, `calendar:page:EventEditor-new`.
같은 페이지에서 날짜나 조건이 바뀌면 ID는 유지되고 EntityInfo가 바뀝니다.
일정 View ID에는 surface와 렌더 위치 경로를 포함합니다. 여러 날짜·pane에 같은 일정이 보여도 각 View는 구분되며 EntityId는 같습니다.
Consumer는 ID를 조립하지 말고 `GetAnnotatedViews`가 반환한 ID를 `FindById`에 전달하십시오.

페이지 EntityInfo의 개념적인 예입니다. wire에서는 `EntityInfo`가 JSON 문자열이며, 그 안의 `Extra`도 JSON 문자열입니다.

```json
{
  "TizenEntity": {
    "Id": "calendar:page:Month",
    "Extra": "{\"schemaVersion\":1,\"page\":{\"surface\":\"Month\",\"selectedDate\":\"2026-09-06\",\"visibleMonth\":\"2026-09\"}}"
  }
}
```

## 좌표·포커스·수명

`ScreenBounds`와 `WindowBounds`는 Annotation 내부가 아닌 `Tizen.Entity.View`의 필드입니다.

- `CalculateScreenPositionSize()`로 finite하고 양수인 실제 변환 후 bounds만 게시합니다.
- `WindowBounds.X/Y = ScreenBounds.X/Y - Window.Default.WindowPosition.X/Y`이며 크기는 같습니다. origin을 읽을 수 없으면 생성 serializer의 non-null 계약 때문에 WindowBounds를 0으로 반환합니다. ScreenBounds를 합성하지 않습니다.
- focus는 NUI `FocusManager.GetCurrentFocusView()`를 사용합니다. 터치/포인터 TextField·TextEditor에 `KeyInputFocus`가 있으면 실제 입력 대상을 우선합니다.
- 현재 subtree 밖 focus를 페이지나 첫 항목으로 대체하지 않습니다. 측정된 annotated focus가 없으면 `Status.Success=false`입니다.
- root 교체 시작과 pause/terminate에 이전 목록을 비웁니다. focus/input/layout/resize는 현재 측정 가능한 tree를 즉시 원자적으로 게시하고 50ms 타이머와 relayout으로 다음 frame을 다시 측정합니다. 단순 조회·포커스 갱신은 현재 목록을 먼저 비우지 않습니다.
- resume는 최신 상태를 다시 렌더합니다. detached/hidden/미측정 view, 0 또는 non-finite bounds는 제외합니다. synthetic `View.Size` fallback은 없습니다.

공통 publication seam은 `Shared/ViewAnnotations/CurrentViewSnapshot.cs`, NUI 수집은 `NuiViewAnnotations.cs`입니다. provider는 immutable snapshot을 잠금으로 교체하고 query마다 generated View DTO를 생성합니다.

## Presentation

`View_ToPresentation`은 generated snapshot의 EntityType/EntityId 및 JSON 형식·크기를 확인합니다. 일정은 event presentation builder를 사용하고, Reminder와 페이지/컨트롤은 해당 snapshot의 현재 필드를 Text/Column에 바인딩합니다. 지원하지 않는 타입, 잘못된 ID, malformed/oversized JSON은 실패합니다.

출력은 기존 **legacy v0.8** split `surfaceUpdate` Template / `dataModelUpdate` Document입니다. canonical v0.9.1이라고 부르지 않습니다. 실제 View → Presentation → DisplayPresentation 왕복은 Common Emulator에서 통과했습니다.

## 검증

Host 테스트는 Calendar의 모든 surface와 신규/수정 모드, Reminder 6개 section의 목록/상세/편집 context, 검색/필터 갱신, 이전 ID 제거, focus/find 일치, 빈 페이지, bounds 거부, pause clear, Presentation malformed/ID mismatch를 검증합니다. NUI actor와 실제 Action RPC는 host 테스트에서 실행하지 않습니다.

설치가 승인된 target에서 각 위 표의 화면마다:

1. 진입 전후 `GetAnnotatedViews`와 native screenshot을 기록하고 surface/조건/Entity가 현재 화면과 일치하는지 확인합니다.
2. 입력·버튼·카드로 D-pad/키보드 및 터치/포인터 focus를 이동합니다. `GetFocusedView`의 ID가 목록 및 `FindById`와 일치해야 합니다.
3. 저장 전 필드를 수정하고 draft가 갱신되되 repository Entity는 저장 전 상태인지 확인합니다.
4. 전 화면의 View ID가 조회되지 않고, overlay 배경이나 숨은 detail이 목록에 없는지 확인합니다.
5. FHD/4K/8K 및 큰 screen/작은 window에서 screenshot과 ScreenBounds/WindowBounds를 비교합니다. resize·inset·pause/resume를 포함합니다.
6. event/Reminder/page/control마다 `View_ToPresentation` 결과를 DisplayPresentation에 보내 실제 렌더를 확인합니다. 잘못된 입력에 대한 typed failure도 기록합니다.

최종 API 14 Action/UI 및 해상도 증거는 [1단계 검증 기록](../../Calendar/docs/STAGE1_VALIDATION.md)을 참고하십시오.

## Authoritative live-view resolution (API 14)

`View_ToPresentation` resolves the supplied View ID, EntityType and EntityId against the atomically published current store. Caller-supplied EntityInfo is not authoritative. Hidden/removed/mismatched identities fail; stale or forged content for a valid visible identity is replaced with current data. Twenty consecutive GetAnnotatedViews/FindById pairs and actual display round trips passed on target; see the stage 1 evidence record.
