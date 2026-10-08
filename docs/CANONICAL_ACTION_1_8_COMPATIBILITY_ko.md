# Canonical Action 1.8 호환성

[English](CANONICAL_ACTION_1_8_COMPATIBILITY.md)

Browser, Calendar, Reminder, PhotoGallery, DisplayPresentation은 tizen-action
1.8 소스 `7ce2a4ca3c0839986d8c6abc8856efaf59f1d3b6`의 canonical default schema와
`action.seq`를 사용합니다. 생성 파일 15개는 전체 category와 Custom dependency를
포함합니다. 앱이 구현하지 않는 method도 전체 category의 positional ID를 유지합니다.
생성 binding을 직접 편집하거나 `action.seq`를 재정렬하지 않습니다.

재생성은 toolchain 소스 `f15c1cda3c985c537addb05161f45518103dcc30`의
actionc/action2tidl과 tidlc 3.1.3을 사용하며 protocol 3 optional presence frame을
유지합니다. `ACTIONC_BIN`, `ACTIONC_ACTION2TIDL`, `ACTIONC_TIDLC`로 도구를,
`ACTIONC_DATA_DIR`, `ACTIONC_ACTION_SEQ`로 canonical catalog와 sequence를 명시합니다.
입력은 앱별 Custom schema를 포함하는 전체 category dependency closure이며 원본
schema bytes를 보존합니다. 각 앱의 `build.sh generate` 후 별도로 빌드합니다.
기존 PATH의 tidlc 2.10.2는 호환 compiler가 아닙니다.

## 구현 경계

재생성된 ABI는 새 앱 기능을 추가하지 않습니다. 아래 method는 domain 변경 전에
명시적으로 unavailable을 반환하며 manifest에서 광고하지 않습니다.

| 앱 | 구현하지 않는 canonical method |
| --- | --- |
| PhotoGallery | AddPhoto, DeletePhoto, StartSlideshow, GetMemories, PlayMemory |
| Calendar | Calendar AddEvent/UpdateEvent; Reminder Add/Update |
| Reminder | Add/Update |
| DisplayPresentation | ShowNudge |

폐기된 ToPresentation과 Presentation Show는 native provider와 manifest에서
제거합니다. 기존 UI와 business 동작을 보존합니다. Display의 local annotation은
기존 wrapped JSON을 유지하지만 canonical 1.8 Presentation entity나 ShowNudge
구현을 뜻하지 않습니다.

지원하지 않는 filter는 빈 값이나 빈 list라도 명시적으로 전달되면 거부하며 조용히
무시하지 않습니다. Reminder State 생략은 아직 Done이 아닌 항목을 선택합니다.
선택한 상태 중 canonical To-do/Done으로 표현할 수 없는 항목이 있으면 limit 적용
전에 Search 전체를 거부합니다. Custom ID resolver도 일부 성공이나 unresolved ID로
대체하지 않고 전체 결과를 거부합니다. 지원하는 ID의 순서와 중복은 유지하고 실제
DueDate 부재는 null로 보존합니다. 실패 시 optional output은 부재로, required
output은 초기화된 값과 Success=false로 반환합니다.

## Managed View 소유권

다섯 앱은 Main 시작 시 application 생성 전에 checked native
`Tizen.NUI.EnvironmentVariable` API로 `DALI_DISABLE_ENTITY_DATA_TIDL=1`을
설정합니다. 이 process 범위 adaptor opt-out으로 앱의 명시적 managed
TizenActionView listener가 소유권을 가집니다. 전역 service를 변경하거나 native
built-in provider의 오래된 wire contract를 수정하지 않습니다.

정확한 installed adaptor/toolkit 소스에서 launchpad preload는 adaptor와 control을
준비하지만 EntityData host를 시작하지 않는 것을 확인했습니다. 일반적인 host 생성은
managed OnCreate 이전 adaptor Start에서 일어납니다. Installed NUI setter는 libc
getenv와 독립적으로 비교하여 확인했습니다.

Canonical View는 WindowBounds를 필수로 요구합니다. Display snapshot은 실제
geometry 부재를 보존하되 표현할 수 없는 View를 반환하기 전에 unavailable로
거부합니다. Annotated query는 list 전체를 거부하고 lookup/focus는 초기화된 실패
View를 반환합니다. 보이는 View를 조용히 제외하거나 성공 좌표를 만들어내지 않습니다.

## 검증 범위

초기 호환성 변경의 portable suite 20개와 genuine installed managed assembly를
사용한 provider 반환값 lane 4개가 통과했습니다. Geometry 후속 host regression,
다섯 owner build와 package 명령, 후속 관련 app/Display portable suite 6개도
통과했습니다. Host provider test는 Parcel/native transport를 검증하지 않습니다.

에뮬레이터에 설치한 최종 package의 DLL 31개와 manifest 5개가 모두 hash 일치했습니다.
다섯 앱의 cold sole-View 호출과 일반 실행 후 View 호출 5개씩 모두 accepted/final이며
정확한 managed 실패 reason, 빈 Id/Extra, 유효한 required object를 반환했습니다
(10/10). 이전 Display cold launch의 잘못된 handler 문제는 이 앱 소유 provider
범위에서 해결되었습니다. Package update 전에 등록한 public watcher는 UPDATE와
RESYNC를 받았고 resident Argot PID3082는 유지되며 update 후 ready catalog는 66입니다.
증거: `argot-capmgr-csharp-owner-device-check.json`,
`argot-capmgr-csharp-owner-update-watch.log`.

최종 readonly catalog/action 10개 재검증도 예상 결과대로 통과했습니다. Browser는
비어 있지 않은 Tab/Page를, Display는 유효한 managed missing-View 실패를 반환했고,
Photo/Calendar/Reminder는 빈 성공 결과와 unsupported-filter 실패를 확인했습니다.
증거: `argot-capmgr-csharp-owner-final-device-receipt.json`. 빈 결과는 비어 있지 않은
entity나 UI 동작을 검증하지 않습니다. 시험한 installed native built-in View serializer는
변경하지 않았습니다. 이 app 범위 소유권 결과는 일반적인 framework 수정이나 전체
visual UI acceptance를 증명하지 않습니다.
