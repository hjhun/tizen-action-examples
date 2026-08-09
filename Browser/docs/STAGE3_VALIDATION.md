# Browser Stage 3 검증 / validation

검증일: 2026-08-09

이 문서는 공개 Common Emulator에서 실행한 Browser 패키지, 실제 WebView, native UI 자동화 결과와 해소되지 않은 target blocker를 분리한다. 이 결과는 TV 제품 승인이나 production signing 증거가 아니다.

## 한국어 결과

### 대상과 패키지

| 항목 | 결과 |
|---|---|
| 대상 | Tizen 10.1 Unified Common Emulator, 1920×1080 |
| 애플리케이션 | `org.tizen.browser` |
| Tizen API package | `Tizen.NET` 14.0.0.19326 |
| package SHA-256 | `c1527f7b0daee622572c840cafcf9f485e6bea522c22f8648068640dd3160159` |
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
| Browser/View typed RPC | 차단 | generated stub/runtime `HasPrivilegeLocal` ABI mismatch가 요청 시 app을 종료함 |
| resolver/ViewAnnotation/legacy Display round trip | 차단 | 위 typed RPC dispatch blocker의 종속 gate |
| canonical A2UI target render | 차단 | 현재 두 문자열 Presentation ABI와 legacy Display parser에는 ordered v0.9.1 transport가 없음 |

Provider discovery 자체는 Browser와 View category 모두 성공했다. 그러나 정확한 Browser `GetCurrent` RPC는 generic error 뒤 앱 종료를 재현했다. fresh `actionc` 전체 category 생성 결과가 tracked generated source와 byte-identical이므로 generated source나 platform schema를 수정하지 않았다.

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

## English summary

The final Browser package builds, packages with the explicit emulator-test-only signer, installs, launches, and renders a real public HTTPS page in the system WebView on a 1920×1080 Tizen Common Emulator. Aurum proved Home, page, invalid-input recovery, tabs, modal trapping/restoration, and exact-one tab close through native screenshots and remote/coordinate input.

This is a partial target result, not full completion. The generated provider crashes at the target RPC boundary because the installed runtime does not provide the generated `StubBase.HasPrivilegeLocal(string, string)` ABI. Therefore typed Browser/View RPC, resolver postconditions, live ViewAnnotation RPC, and legacy DisplayPresentation round trips remain blocked. Canonical A2UI target rendering is independently blocked by the current two-string Presentation transport. Offline UI capture is also blocked because target offline mode disconnects the SDB/Aurum transport used to capture evidence.
