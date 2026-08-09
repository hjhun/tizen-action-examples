# Browser 품질 요구사항

검토일: 2026-08-09

각 항목은 독립 gate다. host 측정값은 Common Emulator 또는 TV 제품 검증으로 대체 보고하지 않는다.

| ID | 영역 | 요구사항·임계값 | 측정 방법 | 실패 처리 |
|---|---|---|---|---|
| NFR-BROWSER-001 | 시작 | Common Emulator cold launch에서 2.0초 이내 physical root와 command band 표시, 3.0초 이내 초기 focus 표시 | launch timestamp + Aurum frame | WebView가 늦어도 shell을 먼저 표시하고 engine-error로 복구 |
| NFR-BROWSER-002 | 입력 응답 | key/pointer/touch 입력 후 focus/pressed cue p95 ≤100ms | 연속 입력 trace와 frame timestamp | 중복 command 방지, busy 상태 표시 |
| NFR-BROWSER-003 | 상태 응답 | navigation submit 후 loading cue ≤100ms, error completion 후 recovery surface ≤500ms | reducer test + target frame | 이전 stable frame 유지 후 bounded error |
| NFR-BROWSER-004 | 취소 | superseded navigation은 100ms 이내 취소 신호, 완료 callback은 절대 최신 상태를 덮지 않음 | deterministic delayed runtime test | `StopLoading`, stale ID discard |
| NFR-BROWSER-005 | timeout | page navigation timeout 15초, retry는 최대 1개 active request | fake clock/target offline probe | typed timeout, Retry/Back 제공 |
| NFR-BROWSER-006 | 동시성 | selected tab당 active navigation 1개, persistence I/O 1개, unbounded queue 없음 | concurrency tests | 최신 intent만 publish |
| NFR-BROWSER-007 | 메모리 | normal tabs 최대 20개; Entity resolver 최대 50 IDs; title 512, URL 4096, details 2048, error 256자 | boundary tests | `invalid_input` 또는 disabled control |
| NFR-BROWSER-008 | 지속성 | normal session JSON schema versioned, atomic replace, ≤256KiB | serialization/failure injection test | malformed/unknown version 폐기 후 home |
| NFR-BROWSER-009 | 개인정보 | cookie, credential, form value, page body, private mode, query/fragment가 report/screenshot/Entity/View/A2UI에 없음 | projection assertions + content scan | fail closed, redact/drop snapshot |
| NFR-BROWSER-010 | 네트워크 보안 | navigation scheme은 HTTP/HTTPS만; HTTPS 성공을 target에서 별도 증명; 인증/인증서/권한 자동 승인 금지 | validation tests + target trace | invalid/unavailable typed state |
| NFR-BROWSER-011 | 접근성 | 모든 enabled control에 고유 accessible label, disabled state 설명, decorative view 제외 | source assertion + Aurum tree 가능 시 query | tree unavailable이면 key/frame 증거와 제한 기록 |
| NFR-BROWSER-012 | 대비 | 일반 텍스트 ≥4.5:1, 큰 텍스트/UI 경계 ≥3:1; focus는 5px 또는 NUI 4px 이상의 outline + scale/surface cue | token contrast script + screenshot review | token 수정 후 재캡처 |
| NFR-BROWSER-013 | 포커스 | 초기 focus 주소, 명시적 graph, disabled skip, modal trap, Back/close focus restoration 100% | reducer/HTML/native input matrix | 상태 완료로 표시하지 않음 |
| NFR-BROWSER-014 | 입력 동등성 | D-pad, keyboard, pointer, touch가 같은 command/reducer를 호출하고 primary/exception flow 결과가 동등 | unit/HTML/Aurum matrix | 차이를 intentional adaptation으로 문서화하지 못하면 실패 |
| NFR-BROWSER-015 | 스케일링 | 1920×1080 canvas를 drawable area에 centered uniform ancestor transform으로 정확히 1회 적용 | 1920×1080, 1280×720, 1440×1080, 2560×1080 geometry tests | invalid geometry에서 기존 root 유지 |
| NFR-BROWSER-016 | inset | `WindowSize`와 `GetInsets()`에서 available area 계산; non-zero/asymmetric inset에서 content가 벗어나지 않음 | host geometry + target capability 시 resize/inset | 새 root를 교체하지 않고 이전 valid frame 유지 |
| NFR-BROWSER-017 | localization | visible string은 중앙화하고 한국어/영어에서 clipping 없음; URL/Action/type은 번역하지 않음 | longest-string 1920×1080/1280×720 review | ellipsis + accessible full label |
| NFR-BROWSER-018 | 수명주기 | pause/terminate에서 cancellation·event unsubscribe·View clear; late callback이 disposed UI 접근 금지 | lifecycle unit test + target relaunch | stale callback drop |
| NFR-BROWSER-019 | ABI | 생성 Browser/View source 수동 수정 금지; whole category 재생성 byte provenance; `action.seq` 기존 순서 보존 | fresh `actionc -a` compare + schema order assertion | generator/runtime 호환 blocker 보고 |
| NFR-BROWSER-020 | A2UI | canonical v0.9.1 lifecycle/catalog 선언; legacy v0.8를 canonical로 오표기 금지; payload 크기 ≤256KiB | JSON schema/profile tests + target round trip | unsupported/malformed/oversized typed failure |
| NFR-BROWSER-021 | View | publish bounds는 finite, width/height >0, 실제 transformed NUI geometry; invisible/stale view 없음 | host mapper tests + target RPC/Aurum | snapshot 제거 |
| NFR-BROWSER-022 | 관찰성 | state, intent ID, bounded error code, elapsed time만 기록; URL query/fragment와 private text 미기록 | log scan | redaction 후만 저장/보고 |
| NFR-BROWSER-023 | 테스트 | 각 FR에 host 또는 target acceptance row; advertised Action마다 success 1건+bounded failure 1건 | `TRACEABILITY.md` completeness check | 누락 FR의 module 완료 금지 |
| NFR-BROWSER-024 | 빌드 | clean host tests/build, Tizen build, TPK integrity/payload/signature, install을 분리 기록 | 실제 command exit code/artifact inspect | 해당 gate만 FAIL/BLOCKED |
| NFR-BROWSER-025 | 호환성 | Public Common Emulator 결과는 Common으로만 표기; TV/product profile은 별도 | 보고서 claim scan | 과장 문구 제거 |
| NFR-BROWSER-026 | 증거 | HTML/native screenshot은 decode 가능하고 의도 viewport/1920×1080, privacy-safe, 상태 label과 provenance 보유 | Pillow decode/dimension/content inspection | invalid/민감 frame 폐기, 재캡처 |
| NFR-BROWSER-027 | 배포 | Browser-owned coherent path만 lock 하에 exact-stage commit/push; generated/build/temp/local state 미포함 | staged path allowlist + `git diff --cached --check` | commit 중단 |

## 측정 기준 해석

- 성능 숫자는 Common Emulator 초기 목표이며 제품 TV의 SLA로 주장하지 않는다.
- 네이티브 frame timestamp를 얻을 수 없는 경우 해당 시간 임계값은 host만 PASS, target은 `NOT VERIFIED`로 남긴다.
- Aurum accessibility tree가 비어 있어도 input/screenshot RPC 성공과 semantic lookup을 혼동하지 않는다.
