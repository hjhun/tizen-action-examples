# 삼성 앱 디자인 기준과 Google A2UI 정책 설계

## 목적

Tizen Action 예제 앱의 제품 디자인 기준과 `DisplayPresentation`의 상호운용 계약을 명확히 분리한다. 도메인 앱은 대응하는 최신 Samsung 앱을 1차 제품·UI 기준으로 삼고, `DisplayPresentation`은 Google A2UI 사양을 따르는 범용 Presentation renderer로 정의한다.

## 정책 결정

### 도메인 앱 디자인

- 대응하는 Samsung 앱이 있으면 해당 앱을 1차 디자인 기준으로 사용한다.
- Browser는 Samsung Internet, PhotoGallery는 Samsung Gallery, Music은 Samsung Music, Video는 Samsung Video를 우선 참고한다.
- 다른 도메인은 가장 가까운 Samsung stock app 또는 One UI system surface를 선택한다.
- 정보 구조, 탐색 방식, 화면 계층, 컴포넌트 상태, 편집·검색·확인 흐름을 참고하되 Samsung 상표, 독점 asset, 사용자 데이터와 화면을 기계적으로 복제하지 않는다.
- Tizen viewport, safe area, remote/D-pad, keyboard, pointer와 touch에 맞게 조정하되 별도의 임의 브랜드나 근거 없는 UI 패턴을 만들지 않는다.
- 대응하는 Samsung 기준이 없을 때만 다른 플랫폼을 보조 근거로 사용하고 선택 이유와 One UI 적용 방식을 문서화한다.

### DisplayPresentation 계약

- A2UI wire format, 메시지 구조, 컴포넌트 모델, 속성, 데이터 바인딩과 버전 의미는 Google A2UI 공식 사양을 기준으로 한다.
- 저장소 전용 A2UI dialect, Samsung 전용 wire format 또는 사양에 없는 필드를 표준처럼 정의하지 않는다.
- 현재 기준은 production release v0.9.1이며 v1.0은 Candidate로 취급한다. 지원 범위는 명시적인 A2UI 버전과 component/property/function matrix로 관리한다.
- 기존 `surfaceUpdate` / `dataModelUpdate` pair는 legacy v0.8 compatibility adapter로 보존하되 canonical v0.9.1로 표현하지 않는다.
- 지원하지 않거나 잘못된 입력은 추측해서 렌더링하지 않고 typed unsupported/invalid 결과로 처리한다.
- Google A2UI 사양 변화는 명시적인 profile version 갱신과 호환성 검토 후 반영한다.

### DisplayPresentation 표현 계층

- A2UI 의미 구조와 데이터는 protocol layer에서 보존한다.
- 검증된 의미 component는 renderer mapping layer에서 Tizen NUI component로 변환한다.
- typography, spacing, shape, color, focus, input과 상태 표현은 Samsung One UI 및 관련 Samsung system surface를 참고한다.
- Presentation payload는 임의 색상, 글꼴, script, HTML, remote asset 또는 제한 없는 layout으로 renderer 정책을 우회할 수 없다.
- `View_ToPresentation`은 현재 렌더링 상태와 의미적으로 동등한 Google A2UI 문서를 반환해야 한다.

```text
Google A2UI Presentation
        |
        v
versioned parser / validator
        |
        v
semantic A2UI component tree
        |
        v
Samsung One UI-adapted Tizen NUI renderer
        |
        v
focus / input / ViewAnnotation / round trip
```

## 수정 범위

다음 문서의 용어와 완료 기준을 함께 정렬한다.

- `AGENTS.md`
- `docs/ONE_UI_PRODUCT_UI_POLICY.md`
- `docs/DASHBOARD.md`
- `.agents/skills/tizen-action-product-development/SKILL.md`
- `DisplayPresentation/docs/ARCHITECTURE.md`
- `DisplayPresentation/docs/A2UI_ONE_UI_PROFILE.md`

기존 앱 구현이나 생성 Action binding은 이 정책 변경에서 수정하지 않는다. Music과 Video의 기존 HTML 초안도 별도 명시 없이 삭제하거나 재작성하지 않는다.

## 문서별 변경 원칙

- 최상위 지침에는 Samsung 앱 우선 원칙과 Google A2UI/One UI renderer 경계를 간결하게 명시한다.
- 제품 UI 정책에는 앱별 대표 기준과 fallback 규칙을 보강한다.
- A2UI 절은 Google A2UI conformance를 protocol 기준으로, Samsung One UI를 renderer 기준으로 구분한다.
- `DisplayPresentation` 문서 제목과 설명에서 “Samsung One UI A2UI renderer”만 단독으로 강조하는 표현을 “Google A2UI-compatible renderer with Samsung One UI-adapted presentation” 의미로 정리한다.
- 기존에 기록된 검증 결과와 미검증 상태는 변경하지 않는다.

## 검증 기준

- 관련 문서 전체에서 Samsung 디자인 참고와 Google A2UI 계약이 서로 모순되지 않는다.
- Browser, PhotoGallery, Music, Video의 대표 Samsung 앱이 명시된다.
- Google A2UI와 Samsung One UI의 책임 경계가 protocol과 presentation layer로 구분된다.
- `surfaceUpdate`, `dataModelUpdate`, `Presentation`, `View_ToPresentation` 같은 식별자는 번역하거나 임의 변경하지 않는다.
- Markdown link와 `git diff --check`가 통과한다.
- 실행하지 않은 A2UI conformance 또는 target 검증을 새로 통과했다고 주장하지 않는다.

## 범위 밖

- Google A2UI 전체 component catalogue의 즉시 구현
- 기존 Presentation payload의 마이그레이션
- NUI 화면 또는 HTML sample 재설계
- package, emulator 또는 Aurum 재검증
- commit 또는 push
