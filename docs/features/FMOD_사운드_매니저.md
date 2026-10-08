### FMOD 사운드 매니저

- 기능 설명: 협업자 코드가 FMOD 타입과 이벤트 경로를 직접 사용하지 않고 SoundId 및 Unity 위치/오브젝트로 재생을 요청한다.
- 동작 조건 및 처리 흐름: Managers가 Resources의 Sound/SoundCatalog를 로드해 SoundManager에 전달한다. Play/PlayAttached는 단발 효과음, PlayBgm은 2D 음악을 재생한다. BGM/SFX Bus로 공통 음량을 제어한다.
- 구현 상태: 구현, Unity 컴파일 및 Play Mode의 실제 공개 API 호출 검증 완료.
- 로그 규칙: SoundManager와 SoundCatalog의 Debug.Log 계열 안내 및 오류 메시지는 영어로 작성한다.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scripts/Managers/SoundManager.cs | SoundManager | FMOD 호출 캡슐화 및 재생 자원 관리 |
| Assets/Scripts/Managers/Managers.cs | InitializeSingleton | 승인된 설정 에셋 로드 및 사운드 서비스 생성 |
| Assets/Scripts/Managers/SoundCatalog.cs | SoundCatalog / TryGetEvent | ID와 EventReference 및 사운드 종류 연결 |
| Assets/Scripts/Enums/SoundId.cs | SoundId | 협업자가 사용하는 사운드 식별자 |
| Assets/Resources/Sound/SoundCatalog.asset | SoundCatalog | Inspector에서 관리하는 사운드 등록 및 Bus 설정 |

#### 연결 및 협업

- 연결된 기능: 기존 Managers와 ResourceManager, FMOD RuntimeManager, BGM_Bus 및 SFX_Bus.
- 공유하는 코드 및 상태: Managers.Sound 접근을 유지하며, 생성 코드 한 줄을 변경한다. 협업자의 게임 로직 및 기존 SoundTest 컴포넌트와 씬은 수정하지 않는다.
- 중복 또는 충돌 사항: 기존 AudioSource/AudioClip 기반 Play API는 FMOD ID 기반 API로 교체한다. 현재 저장소 소스에서는 기존 Play 호출부가 발견되지 않았으며 Managers의 생성과 Clear 연결만 확인했다. 저장소 외부 및 다른 브랜치의 호출부는 미확인이다.
- 승인 필요 사항 및 결과: 사용자가 SoundManager의 자유로운 수정을 승인했다. 설정 에셋 경로 Assets/Resources/Sound/SoundCatalog.asset은 별도 질문으로 승인받았다. 새 Editor 폴더나 저장되는 Editor 스크립트는 만들지 않는다.

#### 협업자 사용 방법

```csharp
// 위치에서 한 번 재생한다.
Managers.Sound.Play(SoundId.Footstep, transform.position);

// 재생 중 오브젝트의 위치를 따라간다.
Managers.Sound.PlayAttached(SoundId.Footstep, gameObject);

// 한 번의 재생 음량만 배율로 조절한다. 1은 기본, 2는 약 +6dB다.
Managers.Sound.PlayAttached(SoundId.Footstep, gameObject, volume: 2f);

// BGM은 별도 API를 사용한다.
Managers.Sound.PlayBgm(SoundId.Ambience);
Managers.Sound.StopBgm();

// 즉시 정지하려면 false를 전달한다.
Managers.Sound.StopBgm(allowFadeOut: false);

// 공통 옵션 음량은 0~1 범위다.
Managers.Sound.BgmVolume = 0.5f;
Managers.Sound.EffectVolume = 0.8f;

// 매니저가 재생한 효과음만 즉시 정지한다.
Managers.Sound.StopEffects();
```

- 재생 메서드는 요청이 유효하고 시작에 성공하면 true, 등록 누락이나 잘못된 호출이면 false를 반환한다. 협업자 스크립트에는 FMOD using과 EventInstance가 필요하지 않다.
- Play의 위치는 재생 당시 고정 위치다. PlayAttached는 발음체를 따라간다. 단발 이벤트는 시작 직후 release를 요청하여 자연 종료 시 FMOD가 해제한다. 보관 목록의 종료된 핸들은 다음 효과음 요청 또는 Clear에서 제거한다.
- PlayAttached는 발음체 비활성화 시 자동 정지를 보장하지 않는다. 단발음은 자연 종료하며 즉시 일괄 정지는 StopEffects를 사용한다. 오브젝트별 지속음 핸들은 이번 변경에 포함하지 않는다.
- BGM 반복은 FMOD Studio의 Loop Region을 사용한다. 같은 BGM의 중복 요청은 인스턴스를 추가하지 않는다. 페이드 정지 중 같은 BGM 재생 요청은 false로 거부하며 종료 후 다시 요청한다.
- 다른 BGM으로 전환할 때 이전 음악은 즉시 정지한다. 크로스페이드 기능은 포함하지 않는다. StopBgm의 실제 Fade Out 길이는 Studio의 Release 설정에 따른다.
- Bus 음량은 해당 Bus에 연결된 모든 이벤트에 적용된다. Clear/StopEffects는 이 매니저가 소유한 재생만 정지하며, Bank를 언로드하지 않는다. BGM/SFX의 기본 런타임 배율은 1이며 Studio에서 정한 믹스를 그대로 사용한다.
- Managers.Sound는 기존 싱글턴과 동일하게 Play Mode에서만 접근한다. 옵션 저장 및 로드는 기존 UI/설정 담당 코드에서 처리한다.

#### 사운드 담당자 등록 방법

1. FMOD Studio에서 이벤트를 제작하고 기존 적절한 Bus와 Bank에 연결한 뒤 빌드한다.
2. SoundId에 새 항목을 명시적인 고유 숫자로 추가한다. 이미 사용한 숫자는 변경하지 않는다.
3. 승인된 SoundCatalog.asset을 선택하고 Events 배열에 Id, Type(BGM/Effect), Event를 등록한다. None 또는 중복된 Id를 사용하지 않는다.
4. FMOD Bus 구조를 바꾸면 해당 에셋의 Bgm Bus Path/Effect Bus Path도 함께 갱신한다.
5. Unity 컴파일 후 협업자는 등록된 SoundId로 호출한다. Play 도중 이미 생성된 SoundManager의 catalog 참조는 동일한 에셋을 사용한다.

현재 등록:

| SoundId | Type | FMOD Event |
| --- | --- | --- |
| Footstep=1 | Effect | event:/Test/TestFootsteps |
| Ambience=2 | BGM | event:/Test/TestAmbience |

#### 기존 API 변경

- Play(string path, SoundType type, float pitch), Play(AudioClip clip, ...), PlayBreakSound를 제거하고 ID 기반 API로 교체했다. 해당 호출이 다른 브랜치에 있으면 함께 마이그레이션해야 한다.
- 무작위 샘플 및 피치 변화는 FMOD Studio 이벤트에서 구성한다. 매니저는 AudioClip 캐시, AudioSource 생성 및 Unity 사운드 풀을 만들지 않는다.
- BgmVolume, EffectVolume, Stop(SoundType), Clear의 접근 방식은 유지한다. Managers의 변경은 SoundCatalog를 로드하여 생성자에 전달하는 한 줄이다.
- 기존 SoundTest/Bgm 및 Footstep은 독립 FMOD 검증 컴포넌트이므로 이번 요청에서 변경하지 않았다. 기존 게임용 SoundManager의 이전 재생 호출은 현재 저장소에서 발견되지 않았다.

#### 검증 결과

- Unity 컴파일 완료, compilationFailed=false, Console 오류 0건.
- 실제 Managers.Sound가 승인된 Resources 에셋을 로드하여 초기화됐다.
- Play의 3D 위치 (3,0.1,0)와 재생 배율 2를 FMOD 인스턴스에서 확인했다.
- PlayAttached 재생 중 발음체를 x=2로 이동했을 때 FMOD의 위치도 x=2로 갱신됐다.
- EffectVolume=0.5 및 BgmVolume=0.4가 실제 SFX/BGM Bus에 반영됐다.
- 같은 BGM을 두 번 요청해도 핸들이 동일했고 PLAYING 상태였다. StopBgm 이후 핸들 해제를 확인했다.
- StopEffects가 두 효과음을 종료하고 보유 목록을 비웠다. Managers.Clear가 BGM과 효과음을 해제했으며 이후 다시 재생할 수 있었다.
- 미등록 SoundId, 잘못된 Type, null 발음체를 각각 false와 안내 경고로 처리했다. 검증에서 발생한 경고 3건은 이 의도된 잘못된 입력에 대한 결과다.
- Play Mode 종료 전 검증용 런타임 음량, 발음체 위치 및 runInBackground 상태를 복구했다. 기존 씬, 기존 사운드 테스트 코드, Material, FMOD 설정 및 Bank는 변경하지 않았다.
- 테스트 프레임워크 코드를 추가하지 않았다. Editor API 작업 및 런타임 관찰에 사용한 일회성 Temp 스크립트는 제거했다.
