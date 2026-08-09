# PhotoGallery 아키텍처 및 제품 계약

> 상태: architect gate 완료 전제 문서이며, 구현/패키지/디바이스 검증 증거는 아직 없다. 플랫폼 API와 실제 target 권한은 구현 시작 시 다시 검증한다.

## 1. 범위와 사용자 가치

PhotoGallery는 `Tizen.Action.Photo`를 제공하는 독립 Tizen NUI 갤러리다. 사용자는 실제 기기 미디어 라이브러리의 사진을 날짜순으로 탐색·검색하고, 상세 정보를 보고, 명시적 확인 후 삭제한다. Agent는 같은 라이브러리를 `Photo` Entity의 안정 ID로 검색·조회하고 현재 화면의 사진 문맥을 ViewAnnotation/A2UI로 얻는다.

- Package/application ID: `org.tizen.photogallery`
- Action category: `Tizen.Action.Photo` (platform catalog의 provider ID를 재사용하지 않음)
- Entity: `Tizen.Entity.Photo`; stable ID field는 `Id`
- 소유 범위: `PhotoGallery/`만. platform schema 및 다른 app은 변경하지 않는다.

### 비목표

편집, 클라우드 동기화, 얼굴 인식, 공유 서비스 계정, 앨범 쓰기 및 정적 데모 데이터를 제품 기능으로 제공하지 않는다. 정적 fixture는 host test에만 사용할 수 있으며, 설치 target의 제품 흐름은 MediaContent에서 읽은 실제 사진에 의존한다.

## 2. 발견 근거와 계약

현재 platform catalog의 `Tizen.Action.Photo` 순서는 ABI 계약이다.

| Action | Agent 의도 | 구현 범위/후속 확인 |
|---|---|---|
| `Tv_Tizen.Action.Photo_AddImage` | 라이브러리에 추가 | target의 지원되는 미디어 등록 경로가 확인될 때만 advertise; 새 앱은 임의 파일 복사로 흉내 내지 않는다. |
| `Tv_Tizen.Action.Photo_DeleteImage` | 선택한 사진 삭제 | 안정 ID를 실제 media record로 resolve한 뒤 삭제; 없는 ID/권한 없음/실패를 typed status로 반환한다. |
| `Tv_Tizen.Action.Photo_Search` | 키워드·위치·날짜 검색 | bounded query와 결과 상한을 적용하고 UI와 동일한 query service를 사용한다. |
| `Tv_Tizen.Action.Photo_ToPresentation` | 사진 display handoff | 해당 Photo에서 얻은 `Presentation`을 반환한다. |
| `Tv_Tizen.Action.Photo_GetPhotoByIds` | 정확한 Entity refresh | 요청 순서와 duplicate를 보존하고 찾지 못한 ID를 `unresolvedIds`에 명시한다. |

`action.seq`의 위 순서가 generated TIDL method ID를 정한다. provider binding은 `actionc -a Tizen.Action.Photo`로 **category 전체**를 생성하며, 생성물은 수동 수정하지 않는다. 실제 manifest에는 구현·advertise한 정확한 Action만 등록한다. View provider 역시 `actionc -a Tizen.Internal.Action.View`로 전체 category를 생성한다.

`Tizen.Entity.Photo`에는 `Location`, `Date`, `Path`, `Note`가 있다. `Path`와 `Note`는 개인 데이터일 수 있으므로 UI annotation과 A2UI에는 generated `ToJson()`의 canonical snapshot을 쓰되, full local path를 external presentation에 노출할지 target privacy review로 확정한다. 임시로 별도 JSON serializer나 가짜 ID를 만들지 않는다.

## 3. 실제 미디어 경계와 선택지

설치된 TizenFX API 14 참조에는 `Tizen.Content.MediaContent.MediaDatabase`, command 및 reader API가 있다. 다음 두 접근을 비교했다.

1. **선택: MediaContent reader adapter.** `MediaDatabase`/photo query 결과를 읽기 전용 `IMediaLibrary`로 감싸고, platform media ID를 `Photo.Id`로 그대로 사용한다. search/index는 UI thread 밖에서 bounded snapshot으로 수행하고 reader/database는 adapter가 dispose한다. 삭제와 추가는 실제 catalog가 제공하는 command만 사용하며, 지원 API 또는 privilege가 없으면 capability-unavailable typed status와 UI 오류 상태를 보여 준다.
2. **기각: 앱 전용 폴더를 직접 scan하는 .NET file adapter.** portable하지만 MediaContent의 안정 ID·metadata·삭제 ownership을 우회하고 gallery의 정의 capability를 static/local-file mock으로 축소한다. test fixture helper로는 가능하나 제품 source가 될 수 없다.

구현 전 compile probe는 `Tizen.Content.MediaContent`의 image filter, ID, path, delete/add command와 요구 privilege를 실제 selected API pack/target profile에서 확인한다. 지원되지 않는 mutation API를 발견하면 Action은 manifest에 advertise하지 않고, product contract와 blocker를 갱신한다. emulator에 사진이 없으면 empty state는 유효하지만 제품 capability가 검증됐다고 주장하지 않는다.

## 4. 계층과 동시성

```text
NUI App / ActionProvider / ViewActionProvider / MediaContent adapter
                         ↓
                  Photo UseCases
                         ↓
      Photo domain model + immutable library snapshot/query rules
```

| 경계 | 책임 |
|---|---|
| `PhotoGallery.Domain` | `PhotoRecord`, validation, stable-ID lookup order/duplicates, bounded query/result policy, pure UI reducer와 viewport math. Tizen reference 없음. |
| `PhotoGallery.UseCases` | `IPhotoLibrary`, query/delete/import use cases, cancellation, stale-completion sequence gate, immutable snapshot publication. |
| `PhotoGallery.Persistence` | 앱 전용 UI preference only (마지막 selection/filter); media metadata의 병렬 저장소를 만들지 않는다. |
| `PhotoGallery.App` | NUI composition, MediaContent adapter, lifecycle, physical thumbnail loading, focus/render, keyboard/remote/pointer dispatch. |
| `PhotoGallery.ActionProvider` | generated Photo DTO ↔ domain 변환, input bound validation, typed status mapping. |
| `PhotoGallery.ViewActionProvider` | render-time snapshot registry, current focus/bounds lookup, `Annotation.EntityInfo = generatedPhoto.ToJson()`, A2UI. |

UI와 provider는 동일한 `PhotoQueryService`/library instance를 DI로 공유하고 self-RPC를 하지 않는다. scan/search/thumbnail decode는 `async`/`await`와 cancellation token을 사용한다. 새 query, page leave, pause, terminate는 이전 request를 cancel하며 completion sequence가 최신일 때만 UI/annotation snapshot을 publish한다. scan concurrency는 하나, displayed result는 200, resolver IDs는 100, individual ID/query length는 256으로 제한한다. database/reader/thumbnail stream/event subscription은 lifecycle 종료 시 dispose/unsubscribe한다.

## 5. One UI 제품 흐름과 CX

상세 화면 정의는 [`../refs/one-ui-design.html`](../refs/one-ui-design.html)에 있다. pre-existing [`../refs/photo-gallery-design.html`](../refs/photo-gallery-design.html)은 preserved visual exploration이며 외부 stock image를 제품 asset이나 runtime dependency로 채택하지 않는다.

| 화면 | 정상 상태 | 예외/empty | Back 및 focus |
|---|---|---|---|
| Pictures | 날짜 그룹 grid, 선택 카드, search 진입 | loading skeleton, permission/capability error, no photos | 초기 focus는 첫 photo 또는 retry; Back은 app exit policy. |
| Search | query field + 결과 grid | cancel은 Pictures의 기존 focus 복원; no results 안내 | Back은 query clear 후 Pictures. |
| Detail | 실제 image, metadata, delete command | load failure placeholder/return | Back은 원래 grid card로 복원. |
| Delete confirmation | photo 제목/삭제 영향, Cancel/Delete | delete failure는 modal을 닫지 않고 status 제공 | focus trap; Cancel/Back은 origin card로, success는 다음 visible card로. |

Remote/D-pad은 row/column spatial order, Enter activation, Back hierarchy를 사용한다. keyboard는 search text/Enter/Escape를 동일 command로 매핑한다. pointer/touch는 semantic command를 dispatch하여 focus를 활성 actor로 맞춘다. labels, visible 4.5:1 contrast, scalable text, non-color selection indicator, bounded ellipsis/metadata, announceable loading/error text를 적용한다. destructive command에는 항상 confirmation이 필요하다.

## 6. NUI scaling, annotation, A2UI

1920×1080 design canvas를 full-window physical root 아래에 top-left pivot으로 두고 `Window.Default.WindowSize`와 `GetInsets()`으로 drawable viewport를 계산한다. design canvas만 centered uniform scale/offset을 한 번 적용한다. invalid/zero viewport에서는 previous root를 유지하며, resize/inset subscription은 terminate 시 해제한다.

현재 visible photo card와 active detail photo만 annotation registry에 publish한다. View ID는 surface별 안정 ID (`pictures:<photo-id>`, `detail:<photo-id>`)이며 Entity ID와 동일시하지 않는다. 실제 NUI focused View에서 `IsFocused`를 구하고 `CalculateScreenPositionSize()`가 finite/positive일 때만 `ScreenBounds`/`WindowBounds` snapshot을 publish한다. overlay/paused/removed views는 즉시 registry에서 제거한다.

`View_ToPresentation`은 Annotation의 generated `EntityInfo` snapshot에서 같은 photo context를 재사용하여, 별개의 유효 JSON `Presentation.Template` (`surfaceUpdate`)과 `Presentation.Document` (`dataModelUpdate`)를 반환한다. `FindById`, `GetAnnotatedViews`, `GetFocusedView`의 missing/failure result도 nested non-null graphs로 초기화한다.

## 7. 검증 가능한 acceptance와 단계

1. **host:** stable ID/order/duplicate/unresolved, bound validation, query cancellation/stale suppression, concurrent snapshot, delete confirmation reducer, viewport matrix와 invalid geometry tests.
2. **build/codegen:** whole Photo/View generated bindings byte-compare, concrete service compile probe, `dotnet build`.
3. **package:** selected signing mode를 명시하고 manifest/payload/signature/dependency archive를 검사한다.
4. **Common Emulator:** capability preflight, app/provider discovery, each advertised Photo Action의 success + bounded failure, mutation 뒤 resolver/Search postcondition, restart restoration where supported; View discovery/bounds/focus/ToPresentation JSON 검증.
5. **Aurum:** Pictures/loading-or-empty, search result/no-result, detail, focused card, safe delete confirmation/error state를 native screenshot으로 검사하고 valid captures만 `docs/images/`에 저장/전달한다.
6. **separate limitation:** Common Emulator 결과는 TV/product media capability 또는 production signing 검증이 아니다.

## 8. 다음 구현 slice

`PhotoGallery.Domain`과 `PhotoGallery.UseCases`의 Tizen-free model/query seam 및 RED tests를 먼저 추가한다. media adapter, provider generated source, NUI 및 package는 그 후의 dependent slice다.
