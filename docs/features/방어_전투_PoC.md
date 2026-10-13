# 방어 전투 PoC

- 기능 설명: `DefenseTest`에서 중앙 고정 플레이어가 사방의 적을 상대하고, 집중 중 원거리 투사체를 감지하면 극한 회피와 반격을 수행한다. 대나무숲은 주기적인 바람이 닿는 동안에만 흰 윤곽선으로 드러난다.
- 동작 조건 및 처리 흐름:
  - 기존 테스트 환경을 제거하고 반지름 20m 원형 공터와 반지름 20~22m의 대나무숲을 배치한다. `Bamboo` 프리팹 858개의 줄기 중심 간격은 0.6m이며 충돌체 없는 시각 배경이다. 공터 바닥으로 NavMesh를 굽는다.
  - `Player` 프리팹 인스턴스의 위치 이동·대시·회피 이동은 고정한다. A/D 및 마우스 수평 입력으로 수평 시점을 부드럽게 돌린다. 기본 회전 속도 120°/초, 마우스 감도 0.12, 완화 시간 0.1초는 Inspector에서 조정한다.
  - 적은 게임 시간 1~2초마다 공터 가장자리의 임의 NavMesh 위치에서 생성한다. 근거리:원거리 가중치는 4:1이고 생존 상한은 2명이다. 각 적에게 거리·장애물 제한 없는 플레이어 추적을 적용하며 동시 공격을 막지 않는다.
  - Shift를 누르는 동안 집중 게이지를 게임 시간으로 소모해 최대 2초 유지한다. Shift 해제 후 실제 시간 3초에 걸쳐 회복한다. 집중 진입 시 기존 감각 시야 API로 반지름 20m를 한 번 밝힌다.
  - 진입 후 실제 시간 0.3초 동안 새로 생긴 투사체까지 매 프레임 확인한다. Inspector 기본 거리 10m 안의 `Enemy_SingleRanged` 투사체 중 가장 가까운 것을 선택한다. 성공 시 씬의 모든 적 투사체를 지우고 실제 시간 약 0.433초 무적과 `timeScale=0.3`을 적용한다.
  - 극한 회피 중 좌클릭하면 실제 시간 0.5초에 발사자 방향으로 시점을 돌린 뒤 즉시 반격한다. 발사자는 대나무 뒤에 있어도 처치한다. 일반 검기 이펙트를 검 위치에서 5배로 만들고, 검보다 앞에서 시작하는 판정의 도달 거리·폭을 일반 공격 설정의 5배로 확장하여 맞은 근거리 적도 처치한다. 반격 직후 시간 배율을 복원한다. 반격 전 Shift 해제 또는 게이지 소진 시 실제 시간 2초 후 불릿타임을 종료한다.
  - 대나무 바람은 게임 시간 5초마다 새 임의 방향을 선택하고 2초에 걸쳐 숲을 통과한다. 방향별 줄기 투영 거리의 분포를 사용하여 숲 중앙을 지날 때 전체 대나무 약 30%는 밝은 흰 테두리, 그 양쪽 각각 약 15%는 중심 대비 50% 밝기의 테두리로 표시한다. 띠 밖의 줄기는 렌더러를 꺼 보이지 않게 한다. 세 비율과 밝기는 Inspector에서 조정한다. 불릿타임 중에는 바람도 함께 느려진다.
- 구현 상태: 씬 구성과 코드 연결 완료. Unity CLI의 C# 컴파일 및 씬 저장 성공. 실제 Game 화면의 연출 체감과 교전 플레이 검증은 미확인.

#### 일반 공격 적중 거리 보정 (2026-10-14)

- DefenseTest 플레이 기록에서 좌클릭은 `NormalStarted`와 `StrikeEnded`까지 진행됐다. 입력이나 공격 동작 자체의 차단은 확인되지 않았다.
- 기존 일반 검 판정의 도달 거리는 1.5m인데, `Enemy_SingleMelee`는 근접 패턴 2m와 돌진 패턴 6m를 공격 가능 거리로 취급해 더 먼 곳에서 멈출 수 있었다.
- DefenseTest 생성 근거리 적만 근접 패턴으로 제한하고 근접 공격 시작 거리를 1.2m로 설정했다. 적은 플레이어 검 판정 안까지 접근한 후 공격한다. 공용 프리팹과 다른 씬의 공격 거리·패턴은 유지한다.
- 검증: Unity 6000.3.22f1 CLI에서 C# 컴파일 및 에셋 재가져오기 성공(종료 코드 0, 오류 없음). 수정 후 실제 적중·피해 플레이 검증은 미확인.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| `Assets/Scenes/TestScene/DefenseTest.unity` | 씬 루트 | 원형 바닥, Player, 대나무숲, 적 생성기 연결 |
| `Assets/Scenes/TestScene/DefensePocNavMesh.asset` | NavMeshData | 공터 적 이동용 구운 내비게이션 |
| `Assets/Scripts/DefensePoc/DefensePocPlayer.cs` | `DefensePocPlayer / HandleInput, UpdateFocus, TryBeginExtremeDodge, ExecuteExtremeSlash` | 고정 시점, 집중, 극한 회피와 반격 |
| `Assets/Scripts/DefensePoc/DefensePocSpawner.cs` | `DefensePocSpawner / TrySpawnEnemy` | 적 생성 비율·주기·상한과 추적 설정 |
| `Assets/Scripts/DefensePoc/DefensePocFocusBar.cs` | `DefensePocFocusBar` | 집중 게이지 UI |
| `Assets/Scripts/DefensePoc/DefensePocBambooWind.cs` | `DefensePocBambooWind / BeginWave` | 대나무 바람의 방향·주기·표시 범위 |
| `Assets/Shaders/DefensePocBambooRim.shader` | `BlindBlade/DefensePocBambooRim` | 대나무 줄기의 흰 시점 테두리 |
| `Assets/Scripts/DefensePoc/Editor/DefensePocSceneBuilder.cs` | `DefensePocSceneBuilder / Build` | Unity CLI 씬 배치와 참조 저장 |
| `Assets/Scripts/GameManagers/InputManager.cs` | `InputManager` | PoC Player가 있을 때만 전용 입력 전달 |
| `Assets/Scripts/Player/PlayerMotor.cs` | `PlayerMotor / SetPositionLocked, SetHorizontalFacing` | PoC 위치 잠금과 수평 회전 |
| `Assets/Scripts/Player/KatanaV2/KatanaDamageReceiver.cs` | `KatanaDamageReceiver` | PoC 무적 시간의 피격 차단 |
| `Assets/Scripts/Enemies/EnemySensor.cs` | `EnemySensor / SetAlwaysTrackTarget` | 생성 적에게만 무제한 인지 적용 |
| `Assets/Scripts/Enemies/Attack/EnemyAttackSelector.cs` | `EnemyAttackSelector / SetMeleeOnly` | PoC 생성 근거리 적의 돌진 패턴 제외 |
| `Assets/Scripts/Enemies/Attack/EnemyMeleeAttack.cs` | `EnemyMeleeAttack / SetAttackRange` | PoC 생성 근거리 적의 근접 진입 거리 조정 |
| `Assets/Scripts/Enemies/EnemyProjectile.cs` | `EnemyProjectile / IsFlying, Instigator` | 투사체 판정과 발사자 식별 |

#### 연결 및 협업

- 연결된 기능: 화면 공간 윤곽선, 감각 표현, 플레이어 시스템, 카타나 전투 V2, 적 추적 및 공격 패턴.
- 공유하는 코드 및 상태: 공용 Player·Enemy 컴포넌트에 PoC 분기를 추가했으나 해당 분기는 `DefensePocPlayer` 존재 또는 적 생성기의 명시적 설정일 때만 활성화한다. 기존 프리팹 자체의 인지·공격 설정은 수정하지 않는다.
- 중복 또는 충돌 사항: 기존 `DefenseTest`의 사각 환경과 보스 전용 오브젝트는 이 PoC 환경으로 대체한다. 대나무 흰 테두리는 전용 투명 재질을 사용하며 공용 감각 시야와 독립적으로 제어한다.
- 승인 필요 사항 및 결과: 사용자가 DefenseTest 전용 조작·인지 변경, 기존 환경 제거, 5초 주기 임의 방향의 2초 바람 파도와 게임 시간 기준 진행을 지정했다.
