# Common Emulator TPK 배포 묶음

- 빌드 날짜: 2026-09-08T07:56:56+09:00.
- 소스 기준 commit: `9a951b6dbdb1f91a46a08a21fbd0cb983e151e10`. Calendar/Reminder/PhotoGallery/Browser는 해당 committed source에서 빌드했습니다.
- DisplayPresentation은 이 commit 위의 기존 **미커밋 작업본**에서 빌드했습니다. 소스 변경은 이번 배포 커밋에 포함하지 않으므로 이 commit만으로 renderer 산출물을 재현할 수 없습니다.
- Release / .NET 8, dotnet API14. Manifest 최소 플랫폼 버전10.0과 구분합니다. NuGet 참조는 기존 버전을 유지했습니다.
- 기존 Tizen Studio Common Emulator 시험용 signer 사용. TV/제품 배포 인증을 의미하지 않습니다.

| 앱 | TPK | 크기(bytes) | DLL | 빌드 warnings/errors |
|---|---|---:|---:|---|
| Calendar | [org.tizen.calendar-0.1.0-api14.tpk](org.tizen.calendar-0.1.0-api14.tpk) | 290255 | 7 | 0/0 |
| Reminder | [org.tizen.reminder-0.1.0-api14.tpk](org.tizen.reminder-0.1.0-api14.tpk) | 219734 | 6 | 0/0 |
| PhotoGallery | [org.tizen.photogallery-0.1.0-api14.tpk](org.tizen.photogallery-0.1.0-api14.tpk) | 218071 | 6 | 0/0 |
| Browser | [org.tizen.browser-0.1.0-api14.tpk](org.tizen.browser-0.1.0-api14.tpk) | 248934 | 6 | 227/0 |
| DisplayPresentation | [org.tizen.displaypresentation-0.1.0-api14.tpk](org.tizen.displaypresentation-0.1.0-api14.tpk) | 166661 | 6 | 0/0 |

모두 package/app ID=`org.tizen.<소문자 앱명>`, version0.1.0입니다. [SHA256SUMS](SHA256SUMS)에 전체 SHA256을 기록했습니다. Music/Video는 디자인 참고 파일만 있으며 빌드 가능한 앱이 없어 포함하지 않았습니다.

## 검사 범위

- 5개 앱 빌드·패키징 성공. Browser의 기존 generated nullable 등 경고227개는 보존했으며 generated 원본을 수정하지 않았습니다.
- ZIP CRC, manifest source byte equality, 전체 Release DLL 및 app-owned schema resource byte equality 확인.
- author/distributor 서명10개: JDK XMLDSig로 C14N11 내부 참조를 포함한 digest와 RSA-SHA512 signature 검증 PASS. 전체 payload coverage도 확인했습니다. 인증서 신뢰 체인/폐기 상태/제품 설치 검증은 아닙니다.
- 이번 TPK는 target에 설치하거나 실행하지 않았습니다. 과거 source/native 검증과 새 패키지 검사를 구분합니다.

## 현재 개발 한계

- [Browser P1](../Browser/docs/UI_PARITY.md): 중간 ABI 이행이며 OpenPage 후속 구현은 native event 귀속/terminal 계약 블로커를 유지합니다.
- [Reminder](../Reminder/docs/UI_PARITY.md): FHD compositor-debug iconify focus 사례까지만 검증됐습니다.
- [PhotoGallery](../PhotoGallery/docs/UI_PARITY.md): stable delete target의 제한된 FHD fixture 검증을 포함합니다.
- [Calendar](../Calendar/docs/STAGE1_VALIDATION.md): SystemInfo 초기 sizing, 현재 고해상도/native gate와 새 VM Action readiness 블로커를 유지합니다.
- renderer canonical/overlay 및 각 앱 native8K 등 미검증 사항은 패키징 성공으로 해소되지 않습니다.

## 재생성 및 무결성 확인

저장소 루트에서 앱마다 기존 공용 패키징 경로를 실행합니다. 로컬 dist를 보존하려면 /tmp의 같은 앱 이름 디렉터리 아래 src를 해당 앱 src에 연결하여 동일 스크립트에 전달할 수 있습니다. 서명 시각 등으로 TPK hash가 달라질 수 있습니다.

```bash
for app in Calendar Reminder PhotoGallery Browser DisplayPresentation; do
    bash scripts/package-dotnet-app.sh "$app" || exit
    cp "$app"/dist/*.tpk Packages/ || exit
done
(cd Packages && sha256sum *.tpk > SHA256SUMS)
(cd Packages && sha256sum -c SHA256SUMS)
```
