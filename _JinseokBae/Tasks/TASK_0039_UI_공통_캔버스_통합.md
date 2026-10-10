# TASK_0039 — UI 공통 캔버스 통합

## 목표

PLAN_0005의 화면들을 UIManager 하위 공통 Canvas에서 관리한다.

## 변경

- 화면별 Canvas·CanvasScaler·GraphicRaycaster를 공통 Canvas로 통합한다.
- 화면 프리팹은 RectTransform과 표시·버튼 처리만 유지한다.

## 구현 항목

- [x] 공통 Canvas 및 UI 생성 부모 변경
- [x] 화면 프리팹 전환과 기존 배치 유지
- [x] 컴파일 및 Play UI 검증

- [x] 공통 Canvas 메인 카메라 자동 연결 및 Player 카메라 태그 설정

## 완료 조건

- UIManager 하위 Canvas 하나에서 두 화면이 표시되고 버튼·재시작 동작이 유지된다.
- 프리팹 GUID 및 미저장 씬 편집을 보존한다.
