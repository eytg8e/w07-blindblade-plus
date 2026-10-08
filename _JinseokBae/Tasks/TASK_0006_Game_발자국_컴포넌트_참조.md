# TASK_0006 — Game 발자국 컴포넌트 참조

## 목표

[PLAN_0001](../Plans/PLAN_0001_발자국_시스템.md)의 발자국 컴포넌트 참조를 Game이 보유하고 이벤트는 Emitter가 소유하도록 수정한다.

## 변경

- Game.FootstepEmitter를 통해 컴포넌트에 접근하고 Emitter의 인스턴스 이벤트를 직접 구독한다.
- Game의 중계 이벤트와 NotifyFootstep을 제거하고 테스트 씬 연결을 갱신한다.

## 구현 항목

- [x] Game 컴포넌트 참조 및 Emitter 인스턴스 이벤트 변경
- [x] 시연·기존 테스트·씬 및 생성 코드 연결 수정
- [x] 코드 컴파일과 기능명세 갱신

## 완료 조건

- Game이 참조하는 Emitter에서 발생 캐릭터와 위치를 직접 전달한다.
- 구독 해제 시 같은 Emitter를 사용하며 Game에 중계 API가 없다.

- 검증: 게임·씬 생성·테스트 코드 컴파일 성공, 테스트 씬 Game-Emitter 직렬화 참조 확인 완료, 실제 PlayMode 실행 미확인.
