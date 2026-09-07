# DisplayPresentation

Calendar의 Presentation과 현재 View snapshot을 표시하는 Tizen NUI renderer입니다.
현재 구현은 기존 split Template/Document용 제한된 legacy v0.8 호환 adapter입니다.
공식 A2UI v0.9.1 version/catalog/lifecycle 지원과 투명 overlay는 아직 구현·검증되지 않았습니다.

현재 catalog의 `Tv_Tizen.Action.Presentation_Show`와 공통 View Action 네 개를
제공하며 .NET API 14를 사용합니다. `Column.children.explicitList`, 문자열 JSON
pointer binding과 최대 512개 component를 처리합니다. 화면당 텍스트 네 개를
표시하고 이전/다음 페이지로 이동합니다. ViewAnnotation에는 현재 페이지와
실제로 측정된 활성 컨트롤만 게시합니다.

저장소 루트에서 실행합니다.

```bash
bash DisplayPresentation/build.sh all
python3 DisplayPresentation/tests/check_action_contracts.py
dotnet run --project DisplayPresentation/tests/DisplayPresentation.UseCases.Tests -c Release
bash DisplayPresentation/package.sh
```

패키지: `dist/org.tizen.displaypresentation-0.1.0-api14.tpk`.
패키지 생성은 설치하지 않으며 Common Emulator 시험용 서명을 사용합니다.

- [개발·검증 기록](docs/2026-09-06-interop-followup.md)
- [프로토콜과 렌더링 범위](docs/A2UI_ONE_UI_PROFILE.md)
- [브라우저/NUI 비교 상태](docs/UI_PARITY.md)
- [설치 후 Action·UI 검증 절차](docs/TARGET_VALIDATION.md)

호스트 검사와 세 앱의 Release build/TPK 생성은 통과했습니다. 실제 기기의
Action round trip, NUI 입력·포커스·화면 크기·주석 수명은 별도 검증 대상입니다.
