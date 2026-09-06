# Reminder API 14 및 해상도 개선 계획

상태: 구현 및 검증 종료. [최종 검증 기록](../../Calendar/docs/STAGE1_VALIDATION.md). 사용자 계획 승인: 2026-09-06.

- 기준 저장소: `tizenfx` Gerrit `tizen_10.1`, `6837507e05e22a4f5f4e76f532bd42b583d26709`, Release 14.0.0.19360, 2026-09-04. 2026-09-06 fetch 후 clean detached checkout 확인.
- 제품: 기존 Reminder 목록 → 상세 → 생성/편집 → 완료/삭제 흐름과 앱 소유 예약 simulator 보존. UI와 Provider는 같은 `ScheduleService`를 사용한다.
- 표준 계약: 최신 `default-actions`의 `Tizen.Action.Reminder` 전체 5개 메서드. Query는 `Limit`/도메인 `Category`/안정 ID, Reminder는 `State.State`를 사용한다.
- Custom 계약: 폐기된 Schedule 이름을 광고하지 않는다. 예약 simulator의 추가/취소/조회 및 batch resolver를 앱 소유 `Tizen.Action.ReminderCustom`으로 제공한다. 표준 Broadcast 예약은 실제 TV 방송 backend 의미이므로 simulator에 그대로 사용하지 않는다.
- 경계: generated binding → 상속 provider(검증/변환) → use case → domain/persistence. 생성 코드는 actionc 출력 그대로 보존한다. 별도 P/Invoke/Interop을 추가하지 않는다.
- 해상도: 초기화 시 공개 `Tizen.System.Information.TryGetValue`로 물리 화면을 읽는다. 실제 drawable area는 `WindowSize`/`GetInsets`로 결정한다. 1920×1080 디자인 단위 canvas 한 곳에서 균등 확대하므로 글꼴, 입력, 테두리, 모서리, focus가 함께 변환된다. 원시 화면 픽셀 상수는 사용하지 않는다.
- 대안 검토: 기존 수동 scale helper를 유지하면 Button/TextField 기본 글꼴/포커스 geometry 누락을 계속 추적해야 한다. Calendar와 같은 ancestor transform을 선택한다. 해상도별 UI 복제는 불필요하다.
- resize: 유효하지 않은 창/inset은 기존 화면을 유지한다. 유효한 resize는 canvas만 변환하여 편집 내용과 focus를 보존한다. Annotation은 측정된 양수 유한 bounds만 게시한다.
- 검증: RED→GREEN host tests, API14 build, TPK payload/signature, 실제 action-tool 각 Action 성공/실패 및 mutation query 후조건, Aurum 입력·화면·Annotation. FHD 1920×1080/UHD 3840×2160/DCI4K 4096×2160/8K 7680×4320를 별도 기록한다. 호스트 계산 검증을 native 검증으로 보고하지 않는다.
- 외부 기능: Common Emulator 예약은 명시적 simulator이며 실제 튜너/녹화 검증을 주장하지 않는다.

## UI 기준 및 adaptation

2026-09-06 확인한 [Samsung 공식 Reminder 사용법](https://www.samsung.com/us/support/answer/ANS10003651/)은 추가·저장, 상세·편집, 완료, 삭제 확인과 카테고리/완료 목록 흐름을 설명한다. 정확한 앱/One UI 버전은 페이지에서 확인되지 않았다. 기존 3-pane 화면은 Tizen 대형 창과 D-pad 입력을 위한 저장소 adaptation이며 Samsung 원본 화면의 기하 복제로 주장하지 않는다. 색/간격은 기존 앱 값을 유지하고 확대 방법만 정리한다.

## 완료 기준

표준/Custom Action와 Entity, 안정 ID 순서 및 중복, State 보존, 저장 후 상태, 현재 View/Presentation, 초기 focus/방향 이동/Enter/Back/편집/삭제 확인을 검증한다. 기존 미커밋 변경을 보존하면서 관련 소스와 재현 가능한 문서/증거만 단계 커밋에 포함하고 빌드·임시·로그는 제외한다. PhotoGallery는 이 단계 검증 및 커밋·푸시 보고를 사용자가 검토하고 공식 승인한 뒤 시작한다.
