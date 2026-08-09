# Browser Stage 3 검증 / validation

검증일: 2026-08-10

이 문서는 공개 Common Emulator에서 실행한 Browser 패키지, 실제 WebView, native UI 자동화 결과와 해소되지 않은 target blocker를 분리한다. 이 결과는 TV 제품 승인이나 production signing 증거가 아니다.

## 한국어 결과

### 대상과 패키지

| 항목 | 결과 |
|---|---|
| 대상 | Tizen 10.1 Unified Common Emulator, 1920×1080 |
| 애플리케이션 | `org.tizen.browser` |
| Tizen API package | `Tizen.NET` 14.0.0.19326 |
| package SHA-256 | `4875827eeaedf08ec016ebea2b0d041d2f2d6cf10a10c1a08b162dd34660288` (current Action/View compatibility verification) |
| signing 경계 | Tizen CLI 내장 emulator-test-only signer. production/default distribution profile로 주장하지 않음 |
| archive | ZIP 무결성, manifest, author/distributor signature, App/provider/domain/use-case/persistence payload PASS |
| 설치/실행 | update install 및 installed app launch PASS |

명시한 개발 signing profile은 로컬에 저장된 인증서 암호와 맞지 않아 실패했다. 암호를 추측하거나 노출하지 않았고, 공개 Emulator 검증은 `-s`를 생략한 CLI의 emulator-test-only signer 경로로만 수행했다.

### 독립 검증 게이트

| 게이트 | 결과 | 증거 경계 |
|---|---|---|
| clean host tests | PASS | Domain, Persistence, UseCases, ActionProvider, App 실행형 test 5개 |
| clean host build | PASS | 0 errors; generated source의 기존 nullable/hiding warning 103개 |
| Tizen C# build | PASS | 0 errors; 같은 generated warning 103개; output metadata와 source compile gate |
| TPK package | PASS | emulator-test-only signing, archive/payload/signature 검증 |
| install/launch | PASS | Common Emulator update install 및 foreground launch |
| real HTTPS WebView | PASS | `https://www.tizen.org/`의 실제 공개 content가 content-only WebView region에 표시됨 |
| native Home/Page/InvalidInput/Tabs/modal | PASS | Aurum PNG, 1920×1080 RGB, non-blank, privacy-safe visual review |
| remote/pointer flow | PASS(범위 제한) | remote 방향키/Enter/Back과 coordinate click의 화면 postcondition 확인 |
| touch parity | 부분 | `tap` status 0만으로 semantic activation을 증명하지 않음; coordinate `click`은 증명됨 |
| accessibility tree | capability 제한 | health/screenshot/input은 동작했지만 tree root가 0이므로 semantic tree를 PASS 처리하지 않음 |
| offline native frame | 차단 | guest offline 전환이 WebView network와 SDB/Aurum transport를 함께 끊어 frame을 캡처할 수 없음 |
| Browser/View typed RPC | PASS (temporary compatibility path) | `tidlc` generated `HasPrivilegeLocal` direct call을 post-generation fail-closed compatibility exception으로 주석 처리; Browser Action 5개 및 View Action 4개 `action-tool` E2E, process liveness, no new crash dump |
| resolver/ViewAnnotation | PASS (Browser scope) | `GetBrowserByIds`, `GetAnnotatedViews`, `GetFocusedView`, actual `FindById`, `View_ToPresentation` target responses verified |
| legacy Display round trip | 차단 | DisplayPresentation target renderer round trip은 이번 Browser package 범위에서 실행하지 않음 |
| canonical A2UI target render | 차단 | 현재 두 문자열 Presentation ABI와 legacy Display parser에는 ordered v0.9.1 transport가 없음 |

Provider discovery 자체는 Browser와 View category 모두 성공했다. 이전 package에서는 `GetCurrent` dispatch가 generated `CheckPrivilege`의 `StubBase.HasPrivilegeLocal(string,string)` 호출로 `MissingMethodException`과 SIGABRT를 일으켰다. 이는 Browser business code가 아니라 `actionc → action2tidl → tidlc` C# UDS generation/runtime ABI mismatch였다. framework generator 수정 전에는 [`RPCPORT_TIDLC_COMPATIBILITY.md`](RPCPORT_TIDLC_COMPATIBILITY.md)의 post-generation fail-closed exception을 적용한다.

### Native UI 상태

| 상태 | 증거 | 판정 |
|---|---|---|
| Home | [`images/native-browser-home-stage3-1920x1080.png`](images/native-browser-home-stage3-1920x1080.png) | 주소 중심 command band, TV-distance hierarchy, 실제 명령 2개, bounded privacy card, unclipped text PASS |
| Loading | [`images/native-browser-loading-stage3-1920x1080.png`](images/native-browser-loading-stage3-1920x1080.png) | context 유지, `LOADING`, progress, Reload disabled PASS; 최종 modal-only patch 직전 동일 navigation source package에서 캡처 |
| Page | [`images/native-browser-page-stage3-1920x1080.png`](images/native-browser-page-stage3-1920x1080.png) | 실제 public HTTPS WebView와 persistent chrome PASS |
| Invalid input | [`images/native-browser-invalid-input-stage3-1920x1080.png`](images/native-browser-invalid-input-stage3-1920x1080.png) | blank input, `CHECK`, Retry/Back/Edit address와 Retry initial focus PASS |
| Tabs | [`images/native-browser-tabs-stage3-1920x1080.png`](images/native-browser-tabs-stage3-1920x1080.png) | ordered rows, selected+focused cue, per-row close, New tab PASS |
| Close confirmation | [`images/native-browser-close-confirmation-stage3-1920x1080.png`](images/native-browser-close-confirmation-stage3-1920x1080.png) | blank title fallback `New tab`, complete description, Cancel initial focus PASS |

모달에서 Right로 Close에 이동한 뒤 Down이 modal 안에 머무는 것을 확인했다. Back은 닫기 요청을 취소하고 호출한 close control로 포커스를 복구했다. 다시 확인한 뒤 Close를 실행하면 정확히 한 탭만 제거되고 3→2 count와 가장 가까운 남은 탭 선택이 표시됐다.

Loading 이미지는 최종 package의 modal title/body와 Home typography를 고치기 직전 package에서 캡처했다. 그 뒤 navigation/loading source는 바뀌지 않았고 최종 package에서 Reload/HTTPS success를 다시 실행했지만, 공개 페이지 응답이 screenshot RPC보다 빨라 재캡처 frame은 READY였다. 이 출처 차이를 숨기거나 READY frame을 LOADING으로 바꾸지 않는다.

### Target 복구와 제한

OfflineMode 실험은 SDB/Aurum까지 끊었다. 원본 VM 설정과 설치 상태를 보존한 일회성 recovery boot로 ConnMan의 `OfflineMode=false`만 복구했고, 이후 device transport, 설정값, public HTTPS 200을 다시 확인했다. 같은 offline 실험을 반복하지 않았고 offline UI를 PASS 처리하지 않았다.

Common Emulator UI는 1920×1080 한 모드에서 검증했다. host viewport test는 non-zero inset, 16:9, 4:3, ultrawide를 포함하지만 이는 native multi-mode evidence를 대신하지 않는다.

### Visual-refinement 재검증

동일한 Common Emulator와 emulator-test-only signer 경로에서 최종 visual package를 다시 build/package/update-install/launch했다. ZIP 무결성, manifest, Browser App/provider/domain/use-case/persistence payload, author/distributor signature를 검사했다. revised Home, real public HTTPS Page, full-canvas Tabs, split-action close confirmation은 각각 `native-browser-*-visual-1920x1080.png`로 재캡처했으며 [`UI_PARITY.md`](UI_PARITY.md)에서 Samsung reference↔HTML↔NUI 차이를 판정한다.

Remote는 Home/Page→Tabs, open↔close, modal trap, Back 복원, confirm close를 실행했고 tab count는 3→2로 변경됐다. coordinate click과 touch tap은 New tab을 각각 추가해 1→2→3 postcondition을 만들었다. Aurum tree는 다시 `root_count: 0`이므로 semantic accessibility는 여전히 미검증이다. typed RPC/A2UI/offline gate는 재실행하지 않았고 기존 차단 상태를 유지한다.

### Address V2 및 Action/View 재검증

승인된 address option A를 outer visual shell + inset native `TextField`로
구현했다. 최종 emulator-test-only package SHA-256은
`b810559e6347d2314c78e7247dabb767ecef834d3a4a6decf6b8650b0a2f9859`이며,
archive 21 entries의 manifest, Browser App/ActionProvider/ViewActionProvider,
Domain/UseCases/Persistence payload와 author/distributor signature를 검사했다.
update-install/launch 후 restored Page에서는 OSK가 자동으로 열리지 않았고,
pointer click editing에서는 blue shell outline, centered URL, caret와 OSK가
표시됐다. 증거는 `native-browser-home-address-v2-1920x1080.png`와
`native-browser-address-edit-v2-1920x1080.png`다.
Home dock으로 quick-access를 연 뒤 visible-focused `Tizen Docs` card를 선택해
terminal Page로 전환했고, 숨겨지는 Home control focus는 WebView로 이전한다.
Home `New tab` 성공 경로는 Tabs count를 정확히 +1로 만들고 주소/OSK를
활성화하지 않은 채 `Tizen Docs` quick-access blue focus를 복원했다. capacity
failure, cancellation, persistence exception은 기존 Home workspace/focus를 publish
없이 보존한다.

Action/Entity/ViewAnnotation portable gate에서 resolver cardinality를 공개
계약의 1~50 IDs로 교정하고 정확히 50개 허용 boundary까지 고정했다. window
coordinates가 unavailable인 valid View snapshot은 generated parcel wire가 null을
표현하지 못하므로 non-null zero `WindowBounds` sentinel로 projection한다. restored
focus tracker는 정상 hydration, superseding intent 폐기, paused Page의 resume 보존,
focus 성공 후 one-shot 소비를 host에서 검증한다. 이 regression들을 포함해 Browser
host executable tests 5/5, solution 0 warnings/0 errors, Tizen C# build 0 errors가
통과했다. fresh `actionc` Browser/View output은 tracked generated source와 각각
byte-identical했다.

최종 설치본의 Browser/View provider discovery는 다시 PASS했다. 이전 ABI failure를 재현한 뒤, `tidlc`가 생성한 `HasPrivilegeLocal` 직접 호출을 documented fail-closed compatibility exception으로 주석 처리했다. fresh package에서 Browser Action 5개와 View Action 4개를 `action-tool`로 호출했다. current page, Go, ordered resolver, Browser/View presentation, annotated/focused-view discovery와 actual `FindById`는 성공했고, Calendar handoff는 typed `unavailable`을 반환했다. 각 호출은 `isError: false`였고 Browser process는 생존했으며 신규 Browser crash dump는 없었다. legacy DisplayPresentation target round trip과 canonical A2UI target transport는 이번 범위에서 여전히 별도 차단 상태다.

## English summary

The final visual-refinement Browser package builds, packages with the explicit emulator-test-only signer, installs, launches, and renders a real public HTTPS page in the system WebView on a 1920×1080 Tizen Common Emulator. Aurum proved the revised Home, Page, full-canvas Tabs, modal trapping/restoration, pointer/touch New tab, and exact-one tab close through native screenshots and state postconditions. Earlier Stage 3 Loading and InvalidInput frames remain historical evidence and are not relabeled as revised visual-package captures.

The generated C# UDS binding contains a known `tidlc` compatibility defect: it calls `StubBase.HasPrivilegeLocal(string, string)`, which is absent from the installed Public Tizen 10.1 RPCPort runtime. Until the framework generator is fixed, the documented post-generation exception comments out that direct call and fails closed for declared-privilege methods. With the exception applied, all five Browser Actions and all four View Actions succeeded through target `action-tool`, including resolver and ViewAnnotation paths; the Browser process stayed alive and no new crash dump appeared. This does not prove the independently blocked canonical A2UI target render, which requires a negotiated ordered Presentation transport.
