# 독백 UI

- 기능 설명: 협업자가 전달한 문구를 공통 Canvas에 표시하는 재사용 UI이다. 독백 발생 조건과 플레이어 입력 정책은 호출자가 결정한다.
- 동작 조건 및 처리 흐름: Canvas를 포함한 UIManager.prefab을 씬에 미리 배치한다. 세션 초기화는 Canvas 하위 화면을 등록하고 숨긴다. Get<MonologueUI>()는 배치된 화면만 조회하며 생성하지 않는다. Show(message, isLastLine)는 본문과 삼각형 방향을 갱신하고 표시한다. SetText는 표시 상태를 유지하며 본문과 방향을 갱신한다. Hide는 숨김, Shutdown은 문구·표시 상태 정리만 수행하며 객체를 파괴하지 않는다.
- 구현 상태: 정적 디자인과 다음/마지막 대사 표시 완료. 우하단 삼각형은 기본 오른쪽, isLastLine=true이면 아래쪽이다. 0.45초 간격으로 표시/숨김을 반복하며 unscaledDeltaTime을 사용한다. 타이핑·자동 종료·대사 큐·게임플레이 입력 제어는 제공하지 않는다. 편집 상태에서는 독백 화면이 활성·빈 본문이며 플레이 초기화 시 숨겨진다.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scripts/GameManagers/MonologueUI.cs | MonologueUI / Initialize, SetText, Show, Shutdown | 본문 및 표시 수명 관리 |
| Assets/Scripts/GameManagers/MonologueIndicator.cs | SetLastLine, OnPopulateMesh, Update | 삼각형 방향과 깜빡임 |
| Assets/Scripts/GameManagers/MonologuePanelStyle.cs | ModifyMesh | 테두리와 반투명 배경 그라데이션 |
| Assets/Resources/Prefabs/GameManagers/UIManager.prefab | Canvas와 세 화면의 중첩 프리팹 | 씬에 미리 배치하는 공통 UI 원본 |
| Assets/Resources/Prefabs/UI/MonologueUI.prefab | MonologueUI, TMP 본문 | 공통 Canvas 하위에 미리 배치되는 화면 원본 |
| Assets/Scripts/GameManagers/UIManager.cs | Get<T>, Show<T>, Hide<T>, Shutdown | 타입별 배치 화면 조회·표시·세션 정리 |
| Assets/Fonts/ChosunCentennial_otf SDF.asset | TMP_FontAsset | 지정된 기존 한글 폰트 |

#### 연결 및 협업

- 연결된 기능: 게임 씬 공통 구성 및 UIManager / GameUI 계약.
- 공유하는 코드 및 상태: Game.Instance.UIManager를 통해 접근하며 동일 타입은 하나의 화면을 공유한다. 여러 호출자가 문구를 전달하면 마지막 문구로 갱신한다.
- 중복 또는 충돌 사항: 기존 게임오버·문 안내 화면과 별도 타입을 사용한다. 기존 독백 구현은 발견하지 못했다. 폰트·UIDesignTest 씬의 기존 변경과 2단계 UIManager 변경은 보존한다.
- 승인 필요 사항 및 결과: 3단계 구현 요청으로 화면 연결과 최소 API 생성이 승인되었다. 최종 디자인은 이 단계에서 확정하지 않는다.

#### 사용 예

```csharp
Game.Instance.UIManager.Get<MonologueUI>().Show(message);
Game.Instance.UIManager.Get<MonologueUI>().Show(lastMessage, isLastLine: true);
Game.Instance.UIManager.Get<MonologueUI>().SetText(updatedMessage, isLastLine: false);
Game.Instance.UIManager.Hide<MonologueUI>();
```

- Hide는 본문을 유지하므로 UIManager.Show<MonologueUI>()로 다시 표시할 수 있다. 세션 종료 시에는 본문까지 비워진다.
- 호출은 Unity 메인 스레드에서 공통 게임 세션이 준비된 뒤 수행한다. UI 표시 자체는 이동 입력·시간 배율·커서를 변경하지 않는다.
- 삼각형은 시각 표시이며 실제 대사 전환이나 클릭·키 입력은 처리하지 않는다. 표시할 마지막 대사 여부는 호출자가 지정한다.

#### 협업자 사용 및 Inspector 편집

1. 새 게임 씬에는 Assets/Resources/Prefabs/Scene/GameSceneInitializer.prefab 및 Assets/Resources/Prefabs/GameManagers/UIManager.prefab을 배치한다. UIManager와 Canvas는 한 세트만 사용한다. 기존 세션 씬 11개에는 이미 연결되어 있다.
2. 공통 세션 초기화 완료 후 Game.Instance.UIManager.Get<MonologueUI>()로 접근한다. 초기화 전 Get은 등록된 화면이 없어 오류를 반환한다. 씬이 바뀌면 이전 MonologueUI 참조를 계속 사용하지 말고 현재 매니저에서 다시 조회한다.
3. Show(message)는 오른쪽 삼각형, Show(message, true)는 아래쪽 삼각형이다. SetText(message, isLastLine)는 표시 중 본문과 방향을 변경한다. Hide<MonologueUI>()는 문구를 유지한 채 숨기며 Shutdown은 문구까지 비운다. 여러 호출자의 동시 요청은 마지막 문구로 덮어쓴다.
4. 문구는 기본 세 줄 이내로 준비하고 의미 단위 줄바꿈을 사용한다. 자동 페이지 분할이나 네 줄 이상 문구의 넘침 방지는 제공하지 않는다. 이동 입력·커서·시간·실제 대사 진행은 호출자 담당이다.
5. 공통 외형은 MonologueUI.prefab 원본에서 편집한다. Panel의 RectTransform은 창 크기·위치, MonologuePanelStyle은 위/아래 배경 색상·알파와 테두리 색상을 담당한다. Body의 TMP는 본문 스타일, ContinueIndicator의 RectTransform은 삼각형 크기·위치, MonologueIndicator의 Blink Interval은 깜빡임 간격이다. 테두리 두께 2/1은 코드 고정값이다.
6. 씬 인스턴스에서 수정하면 해당 씬의 override가 된다. 전체 씬에 공통 반영하려면 프리팹 원본을 수정한다. 비활성화된 InnerBorder·Background Image를 다시 켜면 불투명 채움이 그라데이션을 덮으므로 그대로 유지한다.

#### 2026-10-12 6단계 최종 점검

- 원격 dev 씬 선택 이후 Stage_3에서 공통 UIManager가 빠진 상태를 확인하여 Assets/Scenes/LevelDesign_HP/Stage_3.unity에 UIManager.prefab 한 개를 재배치했다. 기존 Player·GameSceneInitializer·EventSystem은 재사용한다. Canvas·EventSystem·입력 모듈 참조와 독백·문 안내·게임오버 세 화면 연결을 에디터에서 확인했다. UI 배치 외 씬 변경은 분리하여 보존했으며 이번 재배치의 Play 검증은 미실행이다.

- 최종 프리팹의 본문·삼각형 직렬화 참조, CanvasRenderer, 배경 스타일, 지정 폰트, 1400×224 패널·1280×168 본문 영역 및 raycastTarget=false를 재확인했다. UIDesignTest에는 공통 화면 세 타입이 배치되어 있고 씬은 미저장 변경 없이 유지된다.
- 기존 Play 검증으로 화면 조회·표시·숨김·재초기화·게임오버·재시작·방향·깜빡임을 확인했고 5단계에서 문구 길이와 해상도를 점검했다. 이번 단계는 프리팹 참조와 문서·차이 점검만 수행했으며 테스트 코드를 생성하지 않았다.
- Unity는 컴파일 실패 없이 편집 상태다. 기존 FMOD ERR_MEMORY 때문에 Play 검증을 반복하지 않았다. 실제 물리 버튼 입력·오디오 재생·다른 씬의 전체 플레이와 네 줄 이상 문구 넘침은 남은 제한이다. 누락 적 프리팹·NavMesh·FMOD 문제는 UI 작업 범위에서 수정하지 않았다.
- 협업자 사용 방법과 Inspector 편집 위치를 정리하고 6단계를 마쳤다. 커밋·스테이징·푸시는 수행하지 않았다.

#### 2026-10-12 공통 UI 사전 배치 및 삼각형 검증

- 사용자의 공통 UI 전체 전환 요청에 따라 Game의 UIManager 동적 로드를 제거했다. 세션 초기화 시 씬 UIManager를 연결하고 Canvas 하위의 독백·문 안내·게임오버 화면을 등록한다. 화면 생성·파괴 코드는 UIManager에서 제거했다.
- 세션 Initializer 프리팹을 사용하는 LevelDesign_HP 4개, Map 4개, TestScene 3개 씬에 공통 UIManager 프리팹을 추가했다. UIDesignTest 포함 총 11개다. 모든 씬의 UIManager 1개·화면 3개·삼각형 CanvasRenderer 연결을 에디터에서 확인했다.
- UIDesignTest에서 조회 전후 인스턴스 동일, Shutdown 및 Initialize 이후 객체 보존, 오른쪽/아래쪽 삼각형 정점과 깜빡임 알파 0/1을 확인했다. 게임오버 표시와 재시작 후 Playing·UIManager 1개·화면 3개를 확인했다.
- 검증 중 CanvasRenderer 누락을 발견하여 연결과 RequireComponent를 추가했다. 수정 후 실행·재시작·종료에서 새 오류가 없었다. 기존 NavMesh·FMOD 경고는 남아 있다. 다른 10개 씬의 전체 플레이는 미확인이다.
- 다른 씬의 배치 점검 중 기존 누락 적 프리팹 오류가 MainMap·Stairwell·Stairwell_1에서 확인되었다. 해당 에셋 복구는 수행하지 않았다. 씬 저장이 유발한 무관한 직렬화 변경은 추적 씬의 차이에서 분리·복구하여 공통 UI 프리팹 추가만 남겼다.

#### 검증

- Unity 컴파일 오류·경고 0건. UIDesignTest Play API에서 최초 숨김, 동일 인스턴스 재사용, 공통 Canvas 부모 연결, 지정 폰트, 본문 raycastTarget=false를 확인했다.
- 문구 전달·두 줄 한글 본문·표시·숨김·본문 유지 후 재표시를 확인했다. 표시 전후 게임플레이 입력 허용·시간 배율·커서 잠금은 동일했다.
- Shutdown의 본문 비움·숨김 및 세션 종료의 전체 화면 비활성화, Play 종료 후 새 오류 없음을 확인했다. 테스트 스크립트와 씬 변경은 저장하지 않았다.
- 기존 NavMesh 및 FMOD 포트·종료 경고는 남아 있다. 다른 해상도 및 환경이 밝게 드러나는 장면의 가독성은 후속 검증 대상이다.

#### 정적 디자인 및 4단계 검증

- WORLD OF HORROR의 검은 본문 영역·각진 사각 프레임 원리를 참고했다. 새 디자인 수치이며 레퍼런스의 측정값은 아니다.
- 1920×1080 공통 Canvas 기준 하단 중앙, 패널 1400×224, 하단 여백 16이다. 외곽선은 #777777 두께 2, 내부선은 #242424 두께 1이다. 배경은 위 #060606·알파 217/255, 아래 #1A1A1A·알파 153/255의 수직 그라데이션이다. Panel Image에 MonologuePanelStyle을 연결하여 테두리와 배경을 한 메시로 그린다. 기존 InnerBorder·Background Image는 비활성화하여 뒤의 불투명 채움이 투명도를 막지 않게 했다. 별도 이미지 에셋이나 화면별 Canvas는 추가하지 않았다.
- 본문은 #E0E0E0, 기존 폰트 크기 36, 왼쪽·수직 중앙 정렬이다. 내부 여백 왼쪽 40·오른쪽 80·상하 28, 추가 줄 간격 8, 자간 0이며 AutoSize는 비활성이다. 우하단 삼각형은 26×26, 오른쪽 여백 24·하단 여백 22다. 모든 Graphic의 raycastTarget은 false다. 화자명·초상화·버튼·그림자·발광은 없다.
- UIDesignTest의 실제 1920×1080 Game View에서 평가용 한 줄 및 두 줄 독백을 캡처하여 확인했다. 두 줄 본문의 preferredHeight는 76.81, 본문 영역 높이는 96이며 텍스트 넘침은 없었다. 평가 문구와 런타임 인스턴스는 씬·프리팹에 저장하지 않았다.
- 캡처 이후 Play 종료 과정에서 ThreadsafeLinearAllocator::Deallocate의 Invalid memory pointer 오류가 한 번 기록되었다. 재실행·표시·종료에서는 새 오류가 없었다. 원인과 UI 변경과의 관련성은 미확인이며 프로젝트 환경을 수정하지 않았다.

#### 2026-10-12 크기 확대 피드백

- 사용자가 첨부한 노란 영역의 대략적 범위를 좌우 대칭으로 재해석하여 패널을 1120×144에서 1400×224로 확대했다. 본문은 30에서 36, 삼각형은 14×14에서 26×26으로 확대했다. 이미지의 정확한 측정값이 아니라 요청에 따른 설계값이다.
- UIDesignTest Game View에서 두 줄 본문의 preferredHeight 92.89, 가용 높이 168, 넘침 없음과 패널 X 오프셋 0을 확인했다. 수정 전 씬의 미저장 상태를 보존하며 씬을 저장하지 않았다.

#### 2026-10-12 그라데이션 및 반투명 피드백

- 위는 어둡고 진하게, 아래는 조금 밝고 투명하게 조정했다. 본문·삼각형·테두리는 불투명 상태를 유지한다. 배경 색상은 MonologuePanelStyle Inspector에서 조절할 수 있다.
- UIDesignTest Game View에서 검이 배경 뒤로 비쳐 보이는 결과를 확인했다. 런타임 메시의 위·아래 색상과 알파, 활성 배경 Image 1개, 본문 알파 1을 확인했다. 씬의 기존 미저장 상태는 보존했다.

#### 2026-10-12 5단계 게임 화면 점검

- UIDesignTest에서 평가용 9자 한 줄(높이 45.01), 29자 두 줄(92.89), 82자 자동 줄바꿈 두 줄(92.89)을 확인했다. 가용 높이 168 안에 들어가며 글자 크기 36은 유지했다. 192자 반복 문구는 네 줄·높이 188.65로 영역을 넘었다. 현재 Overflow 모드이므로 긴 본문은 화면 밖으로 넘을 수 있다. 사용 문구는 최대 세 줄로 나누고 필요하면 작성자가 줄바꿈을 지정해야 한다. 자동 분할·축소·잘라내기는 추가하지 않았다.
- 기존 Game View 프리셋을 일시 선택하여 실제 Screen 크기 1366×768, 1920×1080, 2560×1440, 3840×2160에서 같은 문구의 두 줄 배치를 확인했다. QHD Canvas 배율은 1.333333, 4K는 2다. 16:10은 현재 창에서 1997×1248로 관측됐고 패널 네 모서리가 화면 안에 있었다. 선택 프리셋은 원래 Full HD(index 3)로 복원했다.
- 1366×768에서 패널은 약 995.8×159.33 픽셀, 좌우 여백은 각각 약 185.1 픽셀이다. SDF 본문은 읽을 수 있었으며 비정수 배율의 가는 테두리는 보간 영향을 받는다. 완전한 픽셀 정합을 보장하지 않는다.
- 기본 어두운 장면 및 SensoryPerceptionMask.RevealFootstep으로 윤곽선 영역을 표시한 장면을 캡처했다. 캡처를 위해 인지 반경과 지속 시간을 런타임에서만 임시 늘렸다. 검의 윤곽이 반투명 패널 뒤로 보였고 본문을 확인할 수 있었다. 고명도 전체 배경과 실제 소리 재생에 따른 연출은 미확인이다.
- 패널 면적은 1920×1080 화면의 약 15.12%, 높이는 약 20.74%다. 중앙 시야는 남지만 하단 검과 환경은 부분적으로 겹친다. 현재 크기·투명도는 사용자 피드백대로 유지했다. 한글 평가 문구의 대체 글리프는 발견하지 못했다.
- Play 중 FMOD.Studio.System.create의 ERR_MEMORY 및 SystemNotInitializedException이 반복되어 종료했다. 컴파일 실패는 없었지만 오디오 정상 작동까지 검증한 것은 아니다. 씬·폰트·UI 프리팹·환경 설정은 이번 점검에서 수정하지 않았고 테스트 코드를 생성하지 않았다.
