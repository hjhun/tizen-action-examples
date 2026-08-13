# Browser clean redevelopment inventory

검사 시각: 2026-08-11T00:56:17+09:00  
범위: `Browser/`만. Calendar/Reminder는 읽기 전용 진단 대상으로 유지한다.

## 목적과 분류 원칙

이 inventory는 기존 Browser 작업을 삭제·재작성하기 전에 제품 계약과 증거를 보존하기 위한 기준선이다. 항목은 다음 중 하나로 분류한다.

- **승인 계약 입력:** 다음 clean slice가 보존해야 하는 제품, UX, Agent 경계.
- **구현 후보:** 계약과 대조하여 clean core/NUI/provider에서 재검증 또는 재구성할 source/test.
- **역사적 증거:** 이전 package 또는 이전 generated-binding 호환 실험의 결과. 새 build/target 성공으로 재사용하지 않는다.
- **보존 전용:** provenance를 위해 남기되 현재 parity 또는 acceptance PASS 근거로 사용하지 않는다.
- **정리 후보:** canonical product에 매핑되지 않는 generated/cache artifact. 이 inventory slice에서는 삭제하지 않는다.

## 승인 계약 입력

| 주제 | 보존 입력 | clean redevelopment에서 지켜야 할 경계 |
|---|---|---|
| 제품 범위 | [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) FR-BROWSER-001~026 | normal-mode, 1~20 tabs, 실제 `WebView`, bounded navigation/recovery만. Secret mode, account, download, extension, body/cookie/form data는 범위 밖이다. |
| 품질/target 구분 | [`QUALITY_REQUIREMENTS.md`](QUALITY_REQUIREMENTS.md) NFR-BROWSER-001~027 | host/build/package/Common Emulator/Aurum/TV를 독립 gate로 보고한다. private URL fields와 content는 persistence, Entity, View, A2UI에 노출하지 않는다. |
| One UI IA | [`SAMSUNG_ANDROID_UI_REFERENCE.md`](SAMSUNG_ANDROID_UI_REFERENCE.md), [`ONE_UI_REFERENCE.md`](ONE_UI_REFERENCE.md), [`SAMSUNG_MODERNIZATION_V2.md`](SAMSUNG_MODERNIZATION_V2.md) | Samsung Internet의 address/Reload + page-first content + 별도 Tabs + Cancel/red Close 정신 모델을 1920×1080 D-pad/pointer/touch에 번역한다. 승인 방향 A의 compact top surface와 bottom dock을 기준으로 한다. |
| executable UI baseline | [`../refs/one-ui-sample.html`](../refs/one-ui-sample.html) | Home, address edit, Loading, Page, Offline/error, Tabs, close confirmation, focus restoration, keyboard/pointer/touch와 centered uniform canvas를 포함하는 유일한 canonical preview로 재검증한다. |
| architecture | [`ARCHITECTURE.md`](ARCHITECTURE.md) | Tizen-free Domain/UseCases/Persistence, actual NUI/WebView adapter, shared UI/provider services, persist-before-publish, cancellation/stale suppression을 유지한다. |
| Agent contract | [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) FR-BROWSER-019~024 및 [`A2UI_CONTRACT.md`](A2UI_CONTRACT.md) | Existing `Tizen.Action.Browser` 전체 category를 사용한다. resolver order/duplicate semantics, current visible page only, generated `ToJson()` annotation snapshot, canonical A2UI v0.9.1과 separately named legacy v0.8 adapter를 분리한다. |
| current View contract | Goal and `.agents/workflows/actionc-generation.md` | View category는 `Tizen.Action.View`; public methods are `Common_Tizen.Action.View_*`. 전체 category output은 fresh `actionc` output과 byte-identical이어야 한다. |

## 구현 후보와 현재 발견 사항

| 영역 | 현재 항목 | 분류 | clean redevelopment 확인 사항 |
|---|---|---|---|
| Domain/UseCases/Persistence | `src/Browser.Domain`, `Browser.UseCases`, `Browser.Persistence`와 executable tests | 구현 후보 | stable IDs, maximums, redaction, atomic session, cancellation, stale-result suppression, shared snapshot을 RED→GREEN으로 새 evidence에 연결한다. |
| NUI/WebView | `BrowserApplication.cs`, `BrowserChromeView.cs`, `NuiWebViewRuntime.cs`, App tests | 구현 후보 | real `Tizen.NUI.BaseComponents.WebView`를 유지하되 initial focus/OSK, inset-aware one-transform geometry, lifecycle cleanup, runtime error/timeout and input parity를 fresh target evidence로 확인한다. |
| Browser provider | `BrowserActionService.cs`, generated Browser binding | 구현 후보 | `GetCurrent`, `Go`, `ToCalendar`, `ToPresentation`, `GetBrowserByIds`의 typed success/failure and resolver postcondition을 generated whole-category binding에서 다시 검증한다. |
| View provider | `BrowserViewActionService.cs`, generated legacy View binding | 구현 후보/불일치 | source currently imports `RPCPort.TizenInternalActionView`; clean rebuild must regenerate and retarget to current `Tizen.Action.View`, not rename or manually patch the generated file. |
| presentation | `BrowserActionContract` and provider presentation paths | 구현 후보 | same visible redacted snapshot must drive canonical producer and explicit legacy adapter. Canonical target transport remains separately constrained by current two-string Presentation ABI. |
| reference artifacts | `refs/one-ui-sample.html`, current HTML PNGs, `UI_PARITY.md` | 계약 입력 + historical visual evidence | current HTML source is the baseline to browser-verify before any NUI UI edit. Existing screenshots are not new-package parity proof. |

## historical and preserved evidence

| Artifact set | Classification | What it can establish | What it cannot establish for clean redevelopment |
|---|---|---|---|
| `STAGE3_VALIDATION.md` and `UI_PARITY.md` native Common Emulator frames | 역사적 증거 | prior 1920×1080 Common Emulator package/UI input observations and screenshot provenance | current source/package build, current generated provenance, fresh RPC, ViewAnnotation, or current UI parity |
| `native-browser-*-stage3-*`, `*-visual-*`, address V2 PNGs | 역사적 증거 | prior screen/state captures; all inspected PNGs decode and retain their stated dimensions | a fresh installed artifact or unmodified generated binding result |
| untracked `native-browser-home-v2-1920x1080.png`, `native-browser-tabs-v2-1920x1080.png` | 보존 전용 | files decode as 1920×1080 RGB PNGs | provenance, intended state, privacy review, or parity; do not publish/relabel without a recorded capture flow |
| `RPCPORT_TIDLC_COMPATIBILITY.md` and existing modified generated files | historical ABI incident | observed generator/runtime incompatibility and former compatibility experiment boundary | permission to retain any generated-source edit or to claim current target RPC success |
| `TRACEABILITY.md` | historical traceability ledger | previous requirement/source/test/evidence map and named gaps | completion status; its rows must be re-baselined against fresh evidence |

## generated and ownership disposition

1. `Browser/src/Browser.ActionProvider/TizenActionBrowserGenerated.cs` and `Browser/src/Browser.ViewActionProvider/TizenInternalActionViewGenerated.cs` were already modified at tick start. They are preserved as historical input only.
2. The existing View generated filename/namespace reflects the removed internal category. The clean generated category must be `Tizen.Action.View`; its output must be whole-category `actionc` output and unmodified.
3. If fresh unmodified output retains `StubBase.HasPrivilegeLocal` and the target runtime lacks that API, this is a framework generator/runtime blocker. No post-generation compatibility change is allowed.
4. `Browser/src/.graphify_*` and `Browser/src/graphify-out/` are repository-local Graphify/cache artifacts, not product source or evidence. They are **정리 후보** only; no deletion occurs in this inventory slice.

## inventory validation performed

- `git status --short -- Browser` recorded eight modified tracked Browser paths and two untracked native PNGs before this slice; all were preserved.
- Browser tracked/untracked inventory was collected with `git ls-files` and `git ls-files --others --exclude-standard`.
- Existing repository Graphify graph was queried before Browser contract/API analysis. Its result still names legacy `TizenInternalActionView`, so it is historical discovery context rather than current View naming authority.
- All Browser documentation PNGs (including the two untracked files) were decoded with Pillow. Each reported PNG format/mode/dimensions; no image was altered.
- No Browser source, generated binding, package, emulator state, Calendar, Reminder, platform schema, or `action.seq` was changed in this slice.

## next dependency-complete slice

`BROWSER-CONTRACT-002`: consolidate the above approved inputs into one current FR/NFR/Agent/target matrix, explicitly resolve legacy/current View naming in prose, and mark every prior target claim historical before editing the executable HTML baseline.
