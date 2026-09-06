# PhotoGallery 개발 가이드

[English](DEVELOPMENT_GUIDE_Eng.md) · [문서 목록](README.md)

## 제품과 공개 런타임

기준 앱은 Samsung Gallery입니다. 공식 [Samsung Gallery 가이드](https://www.samsung.com/us/support/answer/ANS10002535/)를
2026-09-06 확인하여 Pictures, Albums, 검색, 즐겨찾기, 정보, 삭제, 슬라이드쇼
흐름을 참고했습니다. 정확한 앱 바이너리 버전은 명시되지 않았고 기기별 차이가
있습니다. 1920×1080 화면, 4×2 페이지와 D-pad 순서는 Tizen용 적응입니다.
Samsung 전용 이미지나 Music의 브랜딩·자산은 복사하지 않았습니다.
[승인 설계와 대안](STAGE2_PLAN.md)에 선택 근거를 기록했습니다.

`Tizen.NET 14.0.0.19326`의 공개 API14를 사용합니다. 소스 참조는 Gerrit
`tizenfx/tizen_10.1`의 `6837507e05e22a4f5f4e76f532bd42b583d26709`
(Release 14.0.0.19360)이며 2026-09-06 fetch/sync를 확인했습니다.
manifest는 Common OS profile 10.0, .NET application API version 14입니다.
PhotoGallery native 구현이나 직접 작성한 Interop은 없습니다.

`StorageManager.Storages`로 내부·USB 저장소 경로를 구합니다. MediaDatabase와
MediaInfoCommand로 실제 등록된 사진을 읽고 가져온 복사본을 등록합니다.
폴더를 읽어서 가짜 사진 ID를 만들지 않습니다. 조회·즐겨찾기·보기·재시작 동안
MediaContent ID가 유지되며 새 가져오기는 새로운 복사본과 ID를 만듭니다.
현재 File.StorageType으로 표현할 수 없는 SD-card/network 저장소는 제외합니다.
실물 USB 검증은 미수행입니다.

가져오기는 지원 저장소 아래의 절대 JPEG/PNG 경로, 최대 32 MiB를 받습니다.
심볼릭 링크 경로 구성요소와 잘못된 이미지 헤더를 거부하고,
`Images/PhotoGallery/<random-name>`으로 복사한 뒤 공개 MediaInfoCommand.Add로
등록하고 소유권·제목을 저장합니다. 원본은 수정하지 않습니다.
등록 정보와 경로가 일치하는 앱 소유 복사본만 삭제합니다. MediaContent 삭제
응답 전까지 숨김 백업을 보존하고 실패하면 복원합니다. API12부터 폐기된
MediaInfoCommand.UpdateFavorite 대신 앱 메타데이터로 즐겨찾기를 보존합니다.
JSON을 원자적으로 교체한 후 메모리를 게시합니다. 플랫폼 변경이 일부 반영된
뒤 실패하면 실제 라이브러리를 다시 읽고 실패를 반환하여 후속 조회를 맞춥니다.

메타데이터 읽기는 4 MiB로, 새로운 키 추가는 5,000개 항목까지로 제한합니다.
라이브러리 읽기는 최대
20,000개 레코드를 검사하여 최대 5,000개 사진을 반환합니다. 미디어 오류나
손상된 메타데이터는 UI의 unavailable 상태로 나타납니다. 손상 파일을 보존하며
유효한 파일 복원 후 재시작하면 정상 라이브러리를 다시 읽습니다.
개인 사진·가져온 파일·빌드 결과·비공개 메타데이터·RPC 로그는 커밋하지 않습니다.
`tests/fixtures`의 PNG 8개는 직접 제작한 테스트 전용 풍경 이미지입니다.

## 계층 경계

- Domain: 사진 레코드, 입력 제한, 순서 보존 resolver, 기존 순수 인터랙션 규칙.
- UseCases: 공유 GalleryLibraryService, 직렬화된 갱신·변경, 현재 사진·슬라이드쇼,
  현재 상태 기반 Presentation. 기존 쿼리·refresh 취소 테스트도 호스트에서 실행합니다.
- Persistence: 공개 MediaContent 어댑터와 앱 데이터 JSON 원자 저장.
- ActionProvider: 전체 Photo/PhotoGalleryCustom 생성 stub과 얇은 ServiceBase 상속 구현.
  생성 원본을 수정하지 않습니다.
- ViewActionProvider: 전체 View 카테고리, 원자적 현재 View 저장소, ID 검증과 변환.
- App: NUI 구성, 실제 이미지, 키보드·포인터·D-pad, 포커스, 실측 annotation 게시.
  UI와 Provider는 자기 RPC 없이 하나의 서비스를 공유합니다.

저장소 부모 기준 `../appfw/tizen-action/default-actions`가 계약 원본입니다.
`generate-bindings.py`는 카테고리 전체를 생성합니다. Custom v1 슬롯은
알파벳 fallback 순서인 GetPhotoByIds, SetFavorite입니다. 기존 슬롯의 삽입·재정렬이나
생성 C# 패치를 금지합니다. 확장은 스키마를 바꾸고 필요하면 새 버전 카테고리로
생성합니다. 포매터가 제거할 공백도 생성 원본과 동일하게 보존합니다.

## Action과 Agent 계약

단일 Entity 입력은 `arguments` 자체이며 `photo`, `query`, `presentation`으로
감싸지 않습니다. `ids` 같은 object-schema 입력은 이름을 유지합니다.
annotation의 Entity.ToJson()에는 생성 타입 wrapper가 있고, action-tool RPC
출력은 다른 JSON 표현이므로 둘을 혼동하지 않습니다.

`action-tool execute --json` 요청 예:

```json
{"id":1,"params":{"name":"Tv_Tizen.Action.Photo_AddPhoto","appid":"org.tizen.photogallery","arguments":{"Id":"","Extra":"Lake","File":{"Path":"/opt/usr/home/owner/media/Images/lake.png","StorageType":"internal","MimeType":"image/png"}}}}
```

Photo는 Id, 버전이 있는 Extra JSON(`title`, `album`, `favorite`, `owned`), Date,
중첩 File을 반환합니다. File은 Id, Path, StorageType, Size, ModifiedDate,
MimeType을 포함합니다. Add의 Extra는 최대 200자 선택 제목이고 입력 Photo.Id는
비어 있어야 합니다. 다른 사진 변경·Presentation은 ID로 현재 상태를 다시 찾고
호출자가 보낸 스냅샷을 신뢰하지 않습니다.

Search: Id ≤256자, Keyword ≤256자(제목·앨범·날짜), Category는 빈 값 또는
`Photo`, `Tizen.Entity.Photo`, `org.tizen.photogallery`; Limit 0은 100,
그 외에는 1..200입니다. ID 필터 후 개수를 제한합니다. Custom resolver는
1..100개의 비어 있지 않은 ID(각 ≤256자)를 받아 순서·중복을 보존하고
unresolvedIds를 명시합니다. 상태는 스키마의 Success/Reason이며 실패 시
입력·누락·사용 불가·내부 오류 이유와 초기화된 출력 DTO를 반환합니다.

| Action 접미사(별도 표기 외 표준 Photo) | Agent 의도 / 성공 | 유의미한 실패 | 후속 조회 / UI 효과 |
|---|---|---|---|
| AddPhoto | 로컬 이미지 복사본 가져오기 | 누락·지원 외·저장소 밖 경로, 입력 ID | Search에서 새 MediaContent ID 확인 |
| DeletePhoto | 소유 복사본 삭제 | 누락 ID, 비소유 사진 | Search 없음, resolver unresolved |
| GetCurrent | 현재 사진 식별 | 열린 viewer 없음 | Show·슬라이드쇼 선택 반환 |
| Search | ID·이름·앨범·날짜 탐색 | 과도한 키워드, 잘못된 limit/category | 현재 Photo/File Entity |
| Show | 찾은 사진 확인 | 누락 ID | 상세 이미지, GetCurrent 일치 |
| StartSlideshow | 실제 사진 순회 | 빈 라이브러리, 이미 실행 중 | 타이머 후 GetCurrent 변경 |
| StopSlideshow | 현재 사진 유지 | 실행 중인 슬라이드쇼 없음 | GetCurrent 유지 |
| ToPresentation | 현재 사진 메타데이터 표시 | 누락 ID | Presentation_Show 현재 상태 표시 |
| PhotoGalleryCustom_GetPhotoByIds | 요청 순서대로 ID 해석 | 빈·과도한 ID 목록 | 중복 순서와 unresolved IDs |
| PhotoGalleryCustom_SetFavorite | 사진 즐겨찾기 | 누락·잘못된 ID | Search/resolver Extra.favorite, Favorites 탭 |
| View_GetAnnotatedViews | 현재 렌더링 문맥 탐색 | pause·활성 화면 없음 | 실측 페이지·컨트롤·사진 View |
| View_GetFocusedView | 다음 입력의 포커스 식별 | 현재 annotated focus 없음, pause | 실제 입력·이동 포커스 |
| View_FindById | 동작 전에 View 재확인 | 누락·오래된·과도한 View ID | 현재 스냅샷과 bounds |
| View_ToPresentation | 현재 화면·사진 설명 | 제거된 View, Entity ID 불일치 | 현재 스냅샷 → Presentation_Show |

표준 이름은 `Tv_Tizen.Action.Photo_`, Custom은 `App_Tizen.Action.PhotoGalleryCustom_`,
View는 `Common_Tizen.Action.View_`로 시작합니다. 표준 Photo에는 순서·미해결 ID를
표현하는 일괄 resolver와 즐겨찾기 변경이 없어 Custom으로 분리했습니다.
플랫폼 소유 스키마는 수정하지 않습니다.

Agent 예: “Lake를 찾아 즐겨찾기로 지정” → Photo_Search Keyword `Lake` →
Photo.Id/File 문맥 선택 → Custom_SetFavorite → Search Id에서 favorite=true 확인.
“보고 있는 사진 설명” → View 발견 또는 Photo_GetCurrent → 현재 Photo/Annotation →
ToPresentation → Presentation_Show. “가져온 사진 삭제” → Id/owned 재확인 →
DeletePhoto → Search/resolver에서 없음 확인. UI의 직접 삭제는 확인 모달을 거치며,
명시적으로 요청한 Action 삭제는 이미 받은 명령으로 실행합니다.

## 렌더링, 스케일과 라이프사이클

App의 geometry와 PixelSize는 물리 화면 픽셀이 아닌 **기준 설계 단위**입니다.
초기화에서 SystemInfo `Information.TryGetValue(screen.width/height)`를 기록하고
WindowSize/GetInsets로 실제 drawable 영역을 결정합니다. Common Emulator가
1280×720 SystemInfo를 오래된 값으로 반환해도 실제 Window를 덮어쓰지 않습니다.
가운데 정렬한 단일 ancestor transform으로 자식 배치·이미지 영역·문자·모서리·테두리·
포커스를 한 번만 확대합니다. 배경 root는 실제 전체 Window를 채웁니다.
유효하지 않거나 finite가 아닌 Window/inset에서는 기존 root를 유지합니다.
IME inset 변경은 텍스트와 포커스를 버리지 않고 같은 캔버스를 조정합니다.
FHD=1×, UHD=2×, DCI 4K=2×/좌우 각 128px, 8K=4×입니다.

시작 시 로딩 화면은 Import에 포커스를 두며 스캔 후에도 이를 유지합니다.
기존 포커스가 없으면 첫 사진 또는 빈 라이브러리의 Import를 선택합니다.
Pictures는 4열·페이지당 8개이며 D-pad로 행·열과 주변 컨트롤을 이동하고 Enter로
실행합니다. Viewer Back은 원본 사진 포커스를 복원합니다. Delete/Info/Import는
포커스를 모달 안에 제한하며 Cancel/Close로 시작하고 Back은 호출 컨트롤을 복원합니다.
검색 초안과 적용 쿼리를 분리하며 Apply는 IME를 닫고 버튼을 포커스합니다.
Import 경로 초안은 ViewAnnotation에서 가립니다. 실제 포인터 클릭과 가상 키보드·
D-pad를 검증했으며, Aurum의 빈 accessibility tree는 실측 bounds로 보완했습니다.

페이지·컨트롤·사진 annotation은 생성 Entity.ToJson()을 사용합니다.
CalculateScreenPositionSize의 finite·양수 bounds만 게시합니다. 배치·포커스·입력
변경 직후와 50ms/배치 안정화 후 불변 프레임을 교체합니다. root 제거·pause·종료는
현재 View를 지웁니다. 모달은 하부 컨트롤을 노출하지 않습니다. View_ToPresentation은
현재 View.Id + EntityType + EntityId를 대조하고 입력 EntityInfo는 무시합니다.
삭제되거나 이전 화면의 ID, 위조된 Entity ID는 실패합니다.

Presentation은 설치된 renderer가 지원하는 **legacy A2UI v0.8** 분리
surfaceUpdate Template / dataModelUpdate Document입니다. Text/Column이 현재
제목·날짜·앨범·즐겨찾기·소유권을 바인딩하며 `photogallery-photo` surface ID는
renderer의 식별자 제한에 맞습니다. 경로·위치·메모·이미지 bytes·임의 스타일은
제외합니다. 이것은 메타데이터 Presentation이며 사진 자체는 Gallery NUI에
표시합니다. canonical v0.9.1, 투명 창, Image catalog 지원을 주장하지 않습니다.
Action→display, 사진 View→display, 페이지 View→display는 별도 타깃 검사입니다.

## 재현

```sh
cd PhotoGallery
./test.sh
./build.sh
./package.sh
python3 generate-bindings.py --output-root /tmp/gallery-generated-check
# 패키징 전에 생성된 각 *.cs를 바이트 단위로 비교합니다.
sdb -s emulator-26111 install dist/org.tizen.photogallery-0.1.0-api14.tpk
../.agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui session-start --serial emulator-26111 --port 55061
python3 tests/verify_target_actions.py --keep-fixtures
python3 tests/verify_target_views.py
```

Action 스크립트는 UUID 원본 디렉토리를 만들고 가져온 ID를
`/tmp/photogallery-actions.json`에 기록합니다. --keep-fixtures가 없으면 자신의
복사본·원본 fixture만 정리합니다. 후속 UI 검사 동안만 fixture를 유지하고,
끝나면 기록한 소유 ID를 Action으로 삭제하고 해당 UUID 디렉토리를 정리합니다.
View 스크립트는 설치된 DisplayPresentation과 즐겨찾기 fixture가 필요합니다.
Common Emulator의 Home 실행 실패 경고는 Gallery UI가 아니므로 입력 검증 전에
닫습니다. 임시 RPC 보고서와 캡처는 의도적으로 docs/images에 선택한 것 외에는
/tmp에 둡니다.

[검증 결과](STAGE2_VALIDATION.md)와 [HTML/native 비교](UI_PARITY.md)를 참고하십시오.
8K는 **Unverified on target due to emulator DRM constraint**입니다.
호스트 geometry는 통과했지만 reference emulator에 지원되는 7680×4320 DRM 출력
모드가 없습니다. TV 제품, 실물 USB, 물리 터치는 Common Emulator와 별도 검증입니다.

## 화면 증거와 출처

| Pictures | Detail |
|---|---|
| ![Pictures](images/native-pictures-fhd.png) | ![Detail](images/native-detail-fhd.png) |

2026-09-06/07, Public Tizen 10.1 Unified Common Emulator,
`emulator-26111` FHD / `emulator-26101` UHD·DCI, 앱 `org.tizen.photogallery`.
Aurum 원격 키·네이티브 좌표 입력·screenshot RPC를 사용했습니다. accessibility tree는
빈 결과였고 실측 View bounds와 실제 프레임을 사용했습니다. 자체 제작 PNG를 public
AddPhoto로 가져왔으며 Back/Home 시스템 overlay는 프레임에 남아 있습니다.
원본 크기와 전체 상태별 비교는 UI_PARITY 및 검증 문서에 있습니다.
