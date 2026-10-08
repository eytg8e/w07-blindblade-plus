# AGENTS.md

## 0. Git

- 커밋 메세지 제안만 가능
- git cli 수행 금지

## 1. Comment

- Unity `MonoBehaviour`의 Lifecycle 및 Callback 메서드를 제외한 모든 메서드의 상단에 XML `summary` 주석을 작성한다.
- `summary`에는 다음 내용을 포함한다.
  - 메서드가 수행하는 동작
  - 메서드가 사용하는 주요 입력값
  - 메서드가 반환하는 값 또는 변경하는 주요 상태
- 내부 주석은 짧은 평문 문장으로 작성하며, 2~3문장을 초과하지 않는다.
- 내부 주석에는 이모지와 Markdown 문법을 사용하지 않는다.

```csharp
/// <summary>
/// 이동 입력을 받아 현재 속도를 목표 속도로 변경한다.
/// moveInput을 사용하며, 변경된 속도를 _velocity에 저장한다.
/// </summary>
private void ProcessMovement(Vector2 moveInput)
{
    Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

    // 지면 이동에서는 입력 방향을 즉시 속도에 반영하지 않고 가속도로 처리한다.
    _velocity = Vector3.MoveTowards(
        _velocity,
        direction * _moveSpeed,
        _moveAcceleration * Time.fixedDeltaTime);
}
```

---

## 2. Field

### Inspector 설정값

- Inspector에 노출해야 하는 값만 `[SerializeField]`를 사용한다.
- `[SerializeField] public`은 사용하지 않는다.
- `[SerializeField]`가 필요하지 않은 필드는 `private`으로 선언한다.

### 외부 공개 상태

- 클래스 외부에서 값을 참조해야할 경우에만 `public` 프로퍼티를 사용한다.

---

## 3. Field 순서

### 역할 별 1차 분류

필드는 역할에 따라 [Header("")] attribute를 사용하여 그룹화한다.

그룹은 다음 순서로 배치한다.

1. `const`
2. `static`
3. `[Header("")]`
4. ...

### 접근 제한자 별 2차 분류

각 [Header("")] 그룹 내부의 필드는 다음 순서로 배치한다.

1. `[SerializeField] private`
2. `private/protected`
3. `public`

---

## 4. Access Modifier

### Field

- 모든 필드는 접근 제한자를 명시한다.

### Method

- 모든 메서드는 접근 제한자를 명시한다.
- 단, Unity Lifecycle, Callback 메서드는 접근 제한자를 생략한다.
- 단, Unity Lifecycle, Callback 메서드에 virtual, override, abstract 등 Method Modifier을 사용하는 경우에는 접근 제한자를 명시한다.

---

## 5. Using

`using`은 다음 순서로 작성한다.

1. `System`
2. `UnityEngine`
3. Unity 패키지
4. 외부 패키지
5. 프로젝트 namespace

각 그룹 사이에는 한 줄을 넣는다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using Cinemachine;

using Game.Player;
using Game.System;
```

- 사용하지 않는 `using`은 제거한다.
- 같은 그룹 안에서는 알파벳 순서로 정렬한다.

---

## 6. Naming

- private 필드: `_camelCase`
- public 프로퍼티: `PascalCase`
- 메서드: `PascalCase`
- 지역 변수: `camelCase`
- 상수: `SCREAMING_SNAKE_CASE`
- enum 타입: `PascalCase`
- enum 값: `PascalCase`

---

## 7. Scope

- 요청받은 기능과 직접 관련된 코드만 수정한다.
- 요청받지 않은 클래스, 메서드, 변수의 이름을 변경하지 않는다.
- 요청받지 않은 리팩터링을 하지 않는다.
- 기존 동작을 변경하는 코드는 요청된 경우에만 수정한다.
- 테스트 코드 작성 금지

## 8. Code Structure

- Unity Lifecycle 순서와 Inspector 설정을 고려하여 불필요한 `null` check를 추가하지 않는다.

## 9. Feature Specifications

- 기능 추가 또는 수정 전 `docs/FEATURE_SPEC.md`를 읽고 문서 관리 규칙을 따른다.
- 관련 기능명세가 없으면 해당 규칙에 따라 작성하고, 작업 완료 후 변경 내용을 반영한다.
- 기존 기능이나 다른 작업자의 작업과 중복되거나 충돌하면 수정 전에 사용자 승인을 요청한다.
## 10. 진행 모드와 학습 설명

- 주요 기능이나 설계 변경을 시작하기 전에 A 자동 구현 모드와 B 학습형 구현 모드 중 하나를 묻는다. 사용자가 해당 작업의 모드를 이미 정했다면 다시 묻지 않는다.
- A는 Codex가 분석·설계·구현을 진행하고, B는 구현 전에 핵심 개념과 2~3개 구현 방식을 설명한 뒤 선택을 기다린다. 두 모드 모두 구현 후 설명을 제공한다.
- 다음, 계속, 진행만으로 A를 선택한 것으로 간주하지 않는다. 단순 버그 수정이나 명확한 수치 변경은 불필요한 질문 없이 처리한다.
- 구현 후 목적, 선택한 설계와 대안, 핵심 코드 흐름, 사용한 Unity/C# 개념, 직접 조정할 변수와 게임플레이 영향, 검증 결과와 한계를 설명한다.
- 다른 시스템과 연결되면 실제 인터페이스 이름·데이터·호출 시점·담당 영역과 팀원이 연결할 일을 정리해 공유한다. 구현된 코드에 없는 계약은 구현된 것처럼 설명하지 않는다.
- 앞선 작업의 설계·학습 설명이 빠졌다면 다음 주요 작업 전에 보충한다. 완료 시 남은 작업과 다음 후보를 제시하고 진행 모드를 묻는다.
- Build is King: 기존 동작의 안정성과 검증을 우선한다. Speed is King: 현재 목표에 필요한 최소 범위로 구현한다. Context is King: 프로젝트 구조와 팀별 책임을 먼저 확인하고 새 설계의 이유를 설명한다.