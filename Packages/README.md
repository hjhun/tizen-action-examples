# Common Emulator TPK 배포 묶음

- 빌드 날짜: 2026-09-07T13:25:14+09:00 (호스트 시간).
- 빌드 소스 commit: `91293ea5a629a937983389ee41a7b8d6a3cb47c2`. 세 앱과 공유 소스는 이 commit의 변경 없는 상태에서 빌드했습니다.
- 구성: Release, .NET 8 / `Tizen.NET 14.0.0.19326`.
- 앱의 **.NET API 버전은 14**, manifest의 **최소 플랫폼 버전은 10.0**입니다. 서로 다른 버전 필드입니다.
- 서명: 기존 패키징 스크립트의 Tizen Studio **Common Emulator 시험용 signer** (`tizen package`에서 `-s` 생략). 실제 TV/제품용 서명·배포 검증을 의미하지 않습니다.

| 앱 | TPK 파일 | Package ID = App ID | 버전 | 크기(bytes) |
|---|---|---|---|---:|
| Calendar | [org.tizen.calendar-0.1.0-api14.tpk](org.tizen.calendar-0.1.0-api14.tpk) | `org.tizen.calendar` | 0.1.0 | 290279 |
| Reminder | [org.tizen.reminder-0.1.0-api14.tpk](org.tizen.reminder-0.1.0-api14.tpk) | `org.tizen.reminder` | 0.1.0 | 218849 |
| PhotoGallery | [org.tizen.photogallery-0.1.0-api14.tpk](org.tizen.photogallery-0.1.0-api14.tpk) | `org.tizen.photogallery` | 0.1.0 | 217237 |

SHA-256은 [SHA256SUMS](SHA256SUMS)에 기록했습니다. 사용자가 지정한 세 앱의 배포 TPK만 포함합니다.

## 이번 산출물 검사

- 세 앱 Release 컴파일: 경고 0 / 오류 0; TPK 패키징 성공.
- ZIP CRC, manifest 원본/ID/version/exec/type/API, 실행 DLL과 runtime metadata 검사 PASS.
- fresh Release DLL 전체(Calendar 7 / Reminder 6 / PhotoGallery 6개) 및 app-owned Action/Entity 리소스(Calendar 3 / Reminder 6 / PhotoGallery 2개)가 ZIP 페이로드와 바이트 단위로 일치.
- Author와 distributor 서명 6개: 모든 참조 digest 및 RSA-SHA512 서명 값의 **실제 암호학적 검증 PASS**. JDK XMLDSig로 선언된 C14N11 내부 참조도 검증했습니다. 참조 수는 앱별 author/distributor 순서로 26/27, 30/31, 22/23개입니다.
- 페이로드 참조의 전체 포함 여부와 SHA-512 digest도 별도 확인했습니다. 이 검사는 내장 인증서 공개키 기준이며 기기 신뢰 체인·인증서 폐기 상태·제품 설치 가능성 검증을 대신하지 않습니다.
- 복사 전후 TPK 바이트 및 배포 SHA-256 일치 PASS. 인증서 private key, 로컬 설정, 검증 로그는 포함하지 않았습니다.

**이번에 재빌드한 TPK 자체를 타깃에 재설치하거나 UI/Action을 재실행하지 않았습니다.** 기존 source/runtime 검증은 아래 문서를 참조하며 이번 패키징 검사와 구분합니다.

- [Calendar/Reminder ID 변경 및 FHD 검증](../Calendar/docs/APP_ID_MIGRATION.md)
- [Calendar/Reminder 해상도·플랫폼 제약](../Calendar/docs/STAGE1_VALIDATION.md)
- [PhotoGallery UI/타깃 증거](../PhotoGallery/docs/UI_PARITY.md)

## 재생성 및 체크섬 확인

저장소 루트에서 실행합니다. 생성 Action 바인딩과 앱 소스는 수정하지 않습니다. 서명 시각 등으로 재생성된 TPK의 바이트/체크섬은 달라질 수 있습니다.

```bash
mkdir -p Packages
for app in Calendar Reminder PhotoGallery; do
    bash "$app/package.sh"
    cp "$app/dist/org.tizen.${app,,}-0.1.0-api14.tpk" Packages/
done
(
    cd Packages
    sha256sum org.tizen.calendar-0.1.0-api14.tpk \
        org.tizen.reminder-0.1.0-api14.tpk \
        org.tizen.photogallery-0.1.0-api14.tpk > SHA256SUMS
)
```

받은 파일의 무결성만 확인하려면:

```bash
(
    cd Packages
    sha256sum -c SHA256SUMS
)
```

## 설치

설치할 Common Emulator의 serial을 `SERIAL`에 설정한 뒤 저장소 루트에서 실행합니다.

```bash
: "${SERIAL:?Set the Common Emulator serial}"
tizen install -s "$SERIAL" -n Packages/org.tizen.calendar-0.1.0-api14.tpk
tizen install -s "$SERIAL" -n Packages/org.tizen.reminder-0.1.0-api14.tpk
tizen install -s "$SERIAL" -n Packages/org.tizen.photogallery-0.1.0-api14.tpk
```

구 `org.tizen.actionexamples.*` 패키지가 남아 있는 타깃의 데이터 보존/제거 절차는 위 ID 마이그레이션 기록을 참고하세요.
