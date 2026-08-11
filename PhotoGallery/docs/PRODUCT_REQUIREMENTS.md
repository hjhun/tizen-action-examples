# PhotoGallery 제품 요구사항과 Architect Gate

> 상태: 2026-08-11 Architect Gate 기록. 이 문서는 구현 완료나 target 검증을 뜻하지 않는다.

## 1. 제품 목표와 범위

PhotoGallery는 실제 Tizen MediaContent 사진 라이브러리를 탐색하고 검색·상세 확인·명시적 삭제 확인을 제공하는 Tizen NUI 앱이다. Agent는 플랫폼 `Tizen.Action.Photo` 계약으로 `Tizen.Entity.Photo`를 검색·안정 ID로 재조회하고, 현재 렌더된 사진은 ViewAnnotation으로 발견한다.

- 소유 경로: `PhotoGallery/` 및 이 목표의 무시된 상태 파일만.
- 금지 경로: Browser, Calendar, Reminder, DisplayPresentation, Music, Video, shared docs와 platform schemas.
- 플랫폼 category: `Tizen.Action.Photo`; Entity resolver: `Tv_Tizen.Action.Photo_GetPhotoByIds`.
- 비목표: 클라우드 동기화, 얼굴 인식, 공유 계정, 정적 fixture를 제품 사진으로 사용하는 것, 임의 파일 복사로 Add/Delete를 흉내 내는 것.

## 2. 근거와 One UI 적응

- 일차 제품 근거: Samsung Gallery의 Pictures 탐색, 검색, 사진 상세, 선택 및 삭제 확인 흐름.
- 검토한 공개 근거: Samsung Korea Samsung Gallery 제품 페이지와 Samsung One UI 제품 페이지, 2026-08-09 접근 기록. 공개 페이지가 특정 내부 동작을 보증하지 않는 부분은 Tizen 적응으로 명시한다.
- 채택: Pictures-first 날짜 그룹 grid, 같은 화면 문맥의 검색, detail drill-down, 명시적 destructive confirmation, loading/empty/unavailable 회복.
- 적응: 1920×1080 canvas에서 4열의 큰 카드와 두 가지 focus cue(blue outline + thumbnail border/scale)를 사용한다. 원격/D-pad, keyboard, pointer, touch가 동일 reducer command를 호출한다.
- 기각: 독립 TV dashboard, floating dock, glass/gradient, remote image asset, 가짜 계정/사진 데이터. 이들은 Samsung Gallery mental model과 실사용 미디어 경계를 훼손한다.

## 3. 설계 선택과 경계

| 선택지 | 결과 | 사유 |
|---|---|---|
| MediaContent reader adapter | 채택 | 플랫폼 media ID, metadata ownership, 실제 device library 흐름을 보존한다. |
| 앱 폴더 직접 scan | 기각 | stable ID와 mutation ownership을 우회하며 gallery 핵심 기능을 파일 mock으로 축소한다. |
| legacy v0.8 Presentation adapter | 보존 대상 | 현재 `surfaceUpdate` / `dataModelUpdate` producer와 호환한다. v0.9.1로 부르지 않는다. |
| canonical A2UI v0.9.1 | 이후 별도 협상 slice | negotiated catalog와 lifecycle을 별도로 도입한다. v1.0은 Candidate다. |

의존성 방향은 `NUI / Action provider / View provider / MediaContent adapter → UseCases → Domain`이다. domain과 use case는 Tizen-free로 유지한다. UI와 Action provider는 같은 `IPhotoLibrary` 및 `PhotoQueryService` 인스턴스를 주입받으며 self-RPC를 하지 않는다.

Media scan, search, thumbnail decode는 UI thread 밖에서 cancellation을 전달한다. 새 query, page leave, pause/terminate는 기존 request를 취소하고 최신 request만 publish한다. 검색 결과는 200, resolver ID는 100, ID/query 길이는 256으로 제한한다.

## 4. 기능·상태·입력 acceptance

| 흐름 | 성공 조건 | 실패/복구 및 focus 조건 |
|---|---|---|
| Pictures | 실제 MediaContent 사진을 날짜순 grid로 보여 준다. | loading, empty, unavailable에 명확한 recovery control; 첫 사진 또는 retry가 초기 focus다. |
| Search | query가 같은 `PhotoQueryService`로 bounded search를 수행한다. | cancel/Back은 Pictures와 이전 유효 focus를 복원; no-result에는 Show all photos를 제공한다. |
| Detail | 선택한 실제 사진의 image와 bounded metadata를 표시한다. | image failure는 placeholder와 안전한 Back을 제공한다. |
| Delete | Detail의 Delete는 modal confirmation 뒤에만 MediaContent mutation을 요청한다. | Cancel/Back은 Delete focus를 복원; 실패는 modal과 설명을 유지; 성공은 다음 보이는 card 또는 retry로 이동한다. |
| Actions | 구현·advertise한 각 Action은 성공과 bounded typed failure를 제공한다. | mutation 후 Search 또는 resolver로 postcondition을 확인한다. |
| ViewAnnotation | visible `pictures:<id>` / `detail:<id>`만 publish한다. | 실제 NUI bounds/focus를 사용하고, hidden/paused/modal stale view는 제거한다. |

NUI는 `Window.Default.WindowSize`와 `GetInsets()`로 drawable area를 계산한다. full-window physical root 아래에 1920×1080 top-left design canvas 하나만 centered uniform transform한다. invalid viewport는 현재 root를 유지한다.

## 5. privacy와 Presentation

생성된 `TizenEntityPhoto.ToJson()`에는 `Path`, `Location`, `Note`가 포함된다. 따라서 `Annotation.EntityInfo`에는 계약상 generated snapshot을 사용하되, 외부 Presentation에는 raw path, location, note, thumbnail bytes, account data를 표시하지 않는다. 구현 전에 generated Entity snapshot과 target privacy behavior를 재검토한다.

`Tv_Tizen.Action.Photo_ToPresentation`과 `View_ToPresentation`은 Presentation을 노출하므로 DisplayPresentation round trip이 필수다. 현재 split `surfaceUpdate` Template / `dataModelUpdate` Document는 legacy v0.8 compatibility profile이다. canonical v0.9.1은 공식 `a2ui-project/a2ui` revision `ec97cb0d7499932e67003ffe5b709a3db7e7033a` (2026-08-07, 2026-08-09 inspected)의 `createSurface`, `updateComponents`, `updateDataModel`, `deleteSurface`, catalog negotiation, client `action` lifecycle을 별도 versioned adapter로 사용해야 한다.

## 6. 완료 gate와 순서

1. `refs/one-ui-sample.html` browser interaction verification과 HTML state capture.
2. Tizen-free domain/use-case RED→GREEN tests: order/duplicate/unresolved, bounds, cancellation/stale suppression, reducer delete/focus, viewport.
3. complete `Tizen.Action.Photo` binding을 `actionc -a Tizen.Action.Photo`로 regenerate하고 baseline byte comparison 및 compile probe. 생성된 `HasPrivilegeLocal`이 target ABI에 없을 때만 Calendar와 동일하게 그 호출만 comment하고 `has = false`로 fail-closed 처리한다.
4. NUI/App/View provider implementation, actual MediaContent capability preflight, manifest registration.
5. host build/tests, package payload/signature inspection, Common Emulator Action/Entity/View/A2UI E2E, Aurum UI parity screenshots.

Common Emulator의 host/build/target/UI 증거는 각각 별도 gate다. 실제 MediaContent mutation API 또는 required privilege가 없으면 Add/Delete를 advertise하지 않고 capability-unavailable 상태와 blocker를 기록한다.

## 7. 현재 evidence와 다음 slice

2026-08-11에 `PhotoGallery/` scoped Graphify graph를 `$HOME/.graphify/samba/workspace/tizen-action-examples/PhotoGallery/graphify-out/`에 새로 작성했다. 52개 파일(약 12,095 단어)을 탐지했고 AST 추출은 164 nodes/361 edges, 생성 graph는 162 nodes/206 edges/36 communities였다. 이 실행 환경에는 semantic extraction용 delegated-agent 도구가 없었으므로 해당 graph는 구조적 code discovery용이며 문서 의미 관계의 완전한 audit이 아니다.

같은 날 `./build.sh`를 다시 실행하여 Release build와 host `PhotoGallery.Domain.Tests`, `PhotoGallery.UseCases.Tests`가 성공함을 확인했다(기존 generated nullable 경고 48개, error 0). 이것은 NUI, TPK, target, MediaContent, Action dispatch, ViewAnnotation, A2UI 또는 Samsung Gallery parity 증거가 아니다.

`refs/one-ui-sample.html`은 Pictures, search, detail, delete confirmation, loading/empty/error 및 keyboard command path를 구현한 canonical executable sample이다. 2026-08-11에 Playwright Chromium headless로 실제 runtime 흐름을 검증했다: D-pad card 이동, Pictures → Search의 matching/no-result → Detail → Delete Cancel/Escape focus 복원 → Delete 확인, test-only state injection으로 unavailable/loading 상태, 1920×1080 및 1280×720 reference-canvas scaling이 통과했다. 검증 중 modal Back이 Cancel에 남는 결함과 Retry가 hidden control에 focus를 남기는 결함을 발견해 각각 visible Delete 및 Search로 복원하도록 수정했고, modal D-pad traversal을 modal 내부로 제한했다. browser captures는 [`images/`](images/)에 보관했으며 native NUI 증거가 아니다. Tizen-free domain/use-case의 안정 ID, order/duplicate/unresolved, 입력 상한, stale completion, cancellation, reducer delete/focus 테스트는 이미 존재하고 host baseline에서 통과했다. 다음 strict single-writer slice는 변경된 Action toolchain 또는 target ABI가 제공될 때 complete-category codegen provenance와 compile probe를 재개하는 것이다.
