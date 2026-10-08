### FMOD 사운드 테스트

- 기능 설명: BGM 반복 재생과 캐릭터 기준 3D 발소리를 독립 씬에서 확인한다. 고정 상단 카메라가 이동 영역 전체를 보여주며 투명 충돌벽이 캐릭터의 이탈을 막는다.
- 동작 조건 및 처리 흐름: Inspector의 EventReference를 사용한다. BGM은 하나의 인스턴스를 유지하며 Studio의 Loop Region으로 반복한다. 발소리는 호출마다 인스턴스를 생성해 발음체에 연결하고 종료 후 해제한다. 주황색 Sphere가 진원지를 표시하며 발소리 시작 시 0.2초 동안 확대된다.
- 구현 상태: Unity 컴파일, 실제 Play Mode의 벽 충돌, 화면 범위, 진원지 표시 및 BGM 반복 경계 통과 검증 완료. 청각적 방향 구분과 믹스 밸런스는 직접 청취 필요.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scripts/SoundTest/Bgm.cs | Bgm / PlayBgm, StopBgm, ReleaseBgm | BGM 중복 방지, 정지 및 해제 |
| Assets/Scripts/SoundTest/Footstep.cs | Footstep / PlayFootstep, ReleaseFootstep | 이동 발음체의 3D 재생, Sphere 표시 및 해제 |
| Assets/Scripts/SoundTest/Environment.cs | Environment / ProcessInput, SetSourcePosition | CharacterController 이동, Listener 회전, 사운드 테스트 조작 |
| Assets/Scenes/SoundTestScene.unity | SoundTestScene | 카메라, 청취 캐릭터, 바닥, 투명벽, BGM 및 발소리 진원지 |
| Assets/Scripts/SoundTest/Materials/*.mat | Listener, SoundSource, Floor | 청취 캐릭터와 발음체 구분 및 바닥 표시 |

#### 연결 및 협업

- 연결된 기능: FMOD RuntimeManager, StudioListener, Unity Input System, CharacterController.
- 공유하는 코드 및 상태: 기존 FMOD 프로젝트와 Bank를 활용한다. 기존 SoundManager 및 다른 게임 서비스는 변경하지 않는다.
- 중복 또는 충돌 사항: 게임용 SoundManager와 분리된 테스트 컴포넌트이며 서비스 교체를 하지 않는다. 사용자가 변경한 SoundTest 네임스페이스와 파일명을 유지한다.
- 승인 필요 사항 및 결과: 사용자의 현재 씬 수정, 투명벽 및 시각 표시 추가, BGM Loop 설정 요청에 따라 반영했다. 사용자가 삭제한 Editor 폴더를 다시 만들지 않는다. 일회성 Editor 작업은 Temp의 Pipeline 실행 스크립트로 처리한 뒤 제거한다.

#### 씬 구성

- Listener Camera: 위치 (0,28,5.5), 수직 상단 시점, Orthographic Size=14.5. 카메라는 이동 입력의 대상이 아니다.
- Listener Character: 파란 Capsule과 정면 표시 Cube, CharacterController. 자식 Listener에 StudioListener를 연결하고 실제 청취 방향을 캐릭터 회전에 맞춘다.
- Invisible Walls: 렌더러 없는 BoxCollider 4개. 이동 영역은 X=-8~8, Z=-7~18이며 캐릭터의 반경을 고려해 벽 안쪽에서 멈춘다.
- Test Floor: 충돌 가능한 바닥. Footstep Source의 자식 Footstep Origin은 주황색 Sphere이며 실제 발음체를 따라간다.
- 기존 Capsule 발음체 Renderer는 숨기고 Sphere를 표시한다. 자동 발음체 이동은 반경 5m, 방향 및 거리 시나리오는 기존 배치를 유지한다.

#### FMOD 설정

- 프로젝트: `FMODProject/week06-blindblade/week06-blindblade.fspro`, FMOD Studio/Unity 2.03.15.
- BGM: `event:/Test/TestAmbience`, 2D, Timeline의 0~27.552초에 Loop Region을 추가했다. 설치된 fmodstudiocl의 공식 Studio 스크립트 API로 저장했으며 XML을 직접 편집하지 않았다.
- Desktop의 BGM 및 Master Bank를 재빌드하고 Unity Event Browser에서 IsOneShot=false를 확인했다. Unity의 반복 재시작 타이머는 사용하지 않는다.
- 발소리: `event:/Test/TestFootsteps`, 3D 단발, Min=1m/Max=20m. 기존 이벤트를 변경하지 않았다.
- BGM Fade Out은 기존 Allow Fade Out 설정을 사용하며 실제 페이드 길이는 Studio의 Release 엔벌로프에 따른다. 엔벌로프 편집은 이번 요청에 포함하지 않는다.

#### 직접 사용 방법

1. `Assets/Scenes/SoundTestScene.unity`를 열고 Play Mode에서 Game View를 클릭한다. EventReference 및 신규 컴포넌트 참조는 이미 연결되어 있다.
2. `WASD`: 파란 청취 캐릭터 이동. 좌우 방향키: 캐릭터 및 Listener 회전. `R`: 청취 캐릭터 위치와 방향 초기화.
3. `B`: BGM 재생, `N`: BGM 정지. 자동 재생은 BGM 컴포넌트의 Play On Enable에 따른다.
4. `Space`: 발소리 1회. `T`: 0.7초 간격의 자동 발걸음. 주황 Sphere의 확대를 확인한다.
5. `1` 정면 3m, `2` 왼쪽 3m, `3` 오른쪽 3m, `4` 정면 1m, `5` 정면 15m. 방향은 초기 Listener 방향 기준이고 이동 및 회전 후에는 현재 캐릭터 기준으로 듣는다.
6. `M`: 발음체 자동 이동. `V`: Sphere 표시 전환. 방향 및 거리 청취 검증 시 BGM을 먼저 정지한다.

#### 검증 결과

- Git CLI를 사용하지 않고 초기 index 및 파일 해시를 읽어 사용자 수정 사항을 기록했다. SoundTestScene은 작업 시작 시 저장된 상태였다.
- Editor API로 현재 씬을 수정·저장했다. 기존 네임스페이스와 이름을 보존하고 Editor 폴더를 생성하지 않았다.
- 코드 컨벤션: private 필드 명명 및 명시적 접근 제한자, SerializeField private, Header별 필드 순서, using 그룹, Lifecycle 접근 제한자 생략, 일반 메서드 summary를 확인했다. 변경된 이동 대상과 Sphere 표시 동작을 summary에 반영하고 이전 로그 접두사를 제거했다.
- Play Mode에서 반복 이동을 시도했을 때 X 경계는 약 ±7.45, Z 경계는 약 -6.45/17.45에서 멈췄다. 카메라 viewport의 모든 X/Y 좌표가 0~1 안에 있어 캐릭터가 화면 안에 남는 것을 확인했다.
- 발소리 호출 후 실제 프레임에서 Sphere localScale이 (0.8,0.8,0.8)이 되고 이후 (0.4,0.4,0.4)로 복원됐다.
- BGM 타임라인을 27초로 이동시켜 끝을 통과시켰다. 이후 약 472ms로 돌아오고 동일 핸들의 PLAYING 상태가 유지됐다. StudioListener는 1개다.
- 검증 중에만 런타임 Application.runInBackground를 true로 변경했고 종료 전에 false로 복구했다. 프로젝트 설정은 변경하지 않았다.
- 검증 당시 Unity 컴파일 오류 및 Console 오류는 0건이었다. 실제 소리의 반복 경계 품질과 방향 및 거리 체감은 직접 청취 미확인이다.
