# Calendar 개발 문서

이 디렉터리는 `Calendar` 예제 앱을 Tizen Action Framework 2.0 provider로 개발하고 검증하는 방법을 설명합니다.

## 문서 목록

- [Tizen Action Framework 2.0 개발 가이드](TIZEN_ACTION_FRAMEWORK_2_0_DEVELOPMENT_GUIDE.md)
  - typed Entity/Action 설계
  - generated binding 관리
  - provider 등록과 lifecycle
  - host test, TPK packaging, Emulator Action E2E
  - ViewAnnotation과 A2UI 통합
- [ViewAnnotation 및 좌표 계약](VIEW_ANNOTATION.md)
  - Calendar가 annotation을 게시하는 대상
  - `ScreenBounds`와 `WindowBounds`의 위치와 의미
  - 실제 NUI bounds/focus 수집 방식
  - lifecycle 및 검증 방법

## 관련 상위 문서

- [Tizen Action domain 개발 가이드](../../docs/TIZEN_ACTION_DOMAIN_DEVELOPMENT_GUIDE.md)
- [Tizen Action 2.0 domain app catalog](../../docs/TIZEN_ACTION_2_0_DOMAIN_APP_CATALOG.md)
- [Calendar navigation/search/View 설계](../../docs/specs/2026-08-08-calendar-navigation-search-view-design.md)

## 구현 기준 경로

```text
src/Calendar.Domain/                  Tizen-free Entity·검색·presentation 규칙
src/Calendar.Persistence/             JSON persistence 및 alarm state
src/Calendar.UseCases/                mutation command와 보상 처리
src/Calendar.ActionProvider/          Calendar Action provider
src/Calendar.ScheduleActionProvider/  Schedule reminder provider
src/Calendar.ViewActionProvider/      ViewAnnotation 및 A2UI provider
src/Calendar.App/                     NUI UI와 provider composition root
tests/                                host-compatible test projects
```

문서와 구현이 다를 경우 현재 source, generated schema/binding, device runtime 결과를 우선합니다.
