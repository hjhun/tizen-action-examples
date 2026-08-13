# Browser A2UI 계약과 호환 경계

갱신일: 2026-08-09 (Stage 2D)

## 공식 기준

Browser의 canonical producer는 Google-origin [A2UI 저장소](https://github.com/a2ui-project/a2ui)의 revision [`ec97cb0d7499932e67003ffe5b709a3db7e7033a`](https://github.com/a2ui-project/a2ui/tree/ec97cb0d7499932e67003ffe5b709a3db7e7033a)을 기준으로 한다. 이 revision은 2026-08-07에 commit되었고 2026-08-09에 검사했다. [v0.9.1 protocol](https://github.com/a2ui-project/a2ui/blob/ec97cb0d7499932e67003ffe5b709a3db7e7033a/specification/v0_9_1/docs/a2ui_protocol.md)은 Current Production, v1.0은 Candidate다.

선택한 canonical 계약은 다음과 같다.

- MIME type: `application/a2ui+json`
- version: `v0.9.1`
- catalog: `https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json`
- lifecycle: `createSurface` → `updateComponents` → `updateDataModel`; 제거 시 `deleteSurface`
- semantic subset: Basic Catalog의 `Column`과 `Text`; renderer가 typography, color, spacing, shape와 focus를 결정한다.
- data: 현재 visible normal-mode Browser page의 bounded `id`, public URL, title, details만 사용한다. URL user-info/query/fragment, body, cookie, form, credential, remote asset, HTML/script/style/action은 포함하지 않는다.

`BrowserActionContract.CreatePresentations`는 하나의 최대 256자 display snapshot에서 canonical stream과 legacy compatibility pair를 함께 만든다. 따라서 두 profile은 같은 source state와 redaction/bounds를 사용하며, canonical component/data message는 flat root tree와 data binding을 보존한다.

## Tizen Presentation ABI 경계

현재 generated `Tizen.Entity.Presentation`은 `Template`과 `Document` 문자열 두 개만 제공한다. version/catalog negotiation, ordered message stream, MIME type 또는 lifecycle capability를 표현하는 field가 없다. 현재 DisplayPresentation parser도 `Template.surfaceUpdate`와 `Document.dataModelUpdate` 한 쌍만 받는다. 이 wire는 legacy v0.8 compatibility profile이며 v0.9.1로 부르지 않는다.

따라서 Stage 2D의 경계는 명시적이다.

1. portable Browser use case는 공식 canonical v0.9.1 stream을 생성하고 공식 envelope/Basic Catalog schema로 검증한다.
2. `Browser_ToPresentation`과 `View_ToPresentation`은 현재 shared schema와 DisplayPresentation을 깨지 않기 위해 이름이 드러난 `CreateLegacyDisplayPresentation` adapter만 반환한다.
3. 두 Action 경로는 current visible snapshot만 허용한다. non-current Entity, 위조된 View annotation, loading/error/Tabs/modal/paused/terminated state에는 initialized typed failure를 반환한다.
4. canonical v0.9.1을 두 문자열 Action wire로 조용히 포장하거나 legacy envelope를 canonical로 오표기하지 않는다.

canonical stream을 target `Display_Show`까지 전달하려면 별도의 negotiated Presentation transport/adapter가 필요하다. 이 mission은 shared schema와 DisplayPresentation 변경을 명시적으로 금지하므로 해당 integration은 Browser가 단독으로 해소할 수 없는 blocker다. Stage 3에서는 legacy round trip과 독립적인 provider/View gates를 실행하되 canonical target render 완료를 주장하지 않는다.

## 검증 결과와 남은 gate

- official A2UI v0.9.1 `server_to_client.json`, Basic Catalog, common types schema에 create/components/data/delete 4개 message가 모두 통과했다.
- current DisplayPresentation `A2UiPresentationParser`가 Browser legacy Column/Text tree를 실제로 parse하고 context/title/public URL/details 순서를 보존했다.
- authoritative `default-actions` catalog로 fresh whole-category `actionc -a Tizen.Action.Browser`와 `actionc -a Tizen.Action.View`를 생성했다. 두 pure output은 `HasPrivilegeLocal(b.Sender, item)`를 포함하며 tracked compatibility bindings와 byte-identical하지 않다. pure output은 Common Emulator 10.1 RPCPort ABI failure를 재현하고, tracked post-generation fail-closed exclusion은 target-RPC compatibility evidence일 뿐 canonical provenance가 아니다.
- host에서 Tizen generated service 객체 직접 실행을 시도하면 reference-only `Tizen.Applications.Common` runtime assembly를 load할 수 없다. 이 경계는 portable tests로 성공 처리하지 않으며 Stage 3 installed RPC gate로 남긴다.
- Common Emulator의 typed Action/View RPC, measured bounds/focus, 두 legacy Presentation round trip, DisplayPresentation native render는 아직 미검증이다.
