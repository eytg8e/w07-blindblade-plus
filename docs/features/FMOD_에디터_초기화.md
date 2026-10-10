### FMOD 에디터 초기화

- 기능 설명: Event Browser 초기화 실패에 따른 ERR_MEMORY, ERR_INVALID_HANDLE, ERR_INVALID_PARAM 연쇄 오류와 반복 생성을 방지한다.
- 동작 조건 및 처리 흐름: 시스템 생성과 각 초기화 단계의 반환값을 검사한다. 생성 실패 시 즉시 종료한다. 생성 이후 어느 단계에서든 실패하면 finally에서 해당 네이티브 시스템을 release하고 핸들을 지운다. ERR_MEMORY에는 자원이 필요한 NOSOUND 재생성을 시도하지 않는다.
- 구현 상태: 오류 처리 및 미리보기·Bank 캐시 보호 완료. 실패 상태는 명시적 FMOD/Retry Editor Audio 또는 도메인 재로드까지 유지한다. 이미 관리 핸들을 잃은 네이티브 시스템은 안전하게 열거·해제할 수 없으므로 해당 Unity 프로세스 재시작이 필요할 수 있다.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Plugins/FMOD/src/Editor/EditorUtils.cs | AttemptInitialize | 반환값 검사, 실패 시 생성한 시스템 해제 |
| Assets/Plugins/FMOD/src/Editor/EditorUtils.cs | CreateSystem, RecreateSystem, RetryEditorAudio | 실패 상태 기록, 자동 반복 방지, 명시적 재시도 메뉴 |
| Assets/Plugins/FMOD/src/Editor/EditorUtils.cs | GetMetering | 시스템을 새로 만들지 않고 유효한 시스템과 DSP만 측정 |
| Assets/Plugins/FMOD/src/Editor/EditorUtils.cs | LoadPreviewBanks, PreviewEvent | 실패한 시스템에서 Bank 로드·재생 중단 |
| Assets/Plugins/FMOD/src/Editor/EventManager.cs | UpdateCache | 시스템이 없으면 캐시 시간을 갱신하지 않고 기존 캐시 보존 |

#### 연결 및 협업

- 연결된 기능: FMOD Event Browser, Bank Refresh 및 Timeline 미리보기. 게임의 SoundManager와 StairwellMusic 재생 설정은 변경하지 않는다.
- 공유하는 코드 및 상태: EditorUtils.system과 initializationFailed를 사용한다. Event Browser의 프레임별 GetMetering 호출은 시스템 생성·재시도를 일으키지 않는다. 시스템 생성 실패를 다른 FMOD API 호출의 성공으로 간주하지 않는다.
- 중복 또는 충돌 사항: FMOD Unity SDK 내부의 프로젝트용 수정이다. SDK 업데이트 시 해당 패치 유지 여부를 검토해야 한다. 사용자 Bank·이벤트·음원 편집은 포함하지 않는다.
- 승인 필요 사항 및 결과: 사용자 버그 수정 요청과 기존 자동 구현·커밋 지시에 따라 처리했다. 재시작 전 Play=false 및 모든 열린 씬의 isDirty=false를 확인했다.

#### 검증

- 수정 전 시스템 핸들은 invalid였고 FMOD 할당 메모리는 약 62MB였다. 재연결만으로는 복구되지 않아 대상 Unity 프로젝트를 정상 종료·재시작했다. 이는 실제 물리 RAM 부족을 입증하는 수치가 아니며 네이티브 시스템 자원 한도 문제 가능성을 뒷받침한다.
- 잘못된 outputType을 전달하는 일시적 Editor 실행으로 초기화 실패 32회를 유발했다. 실패 32회 모두 핸들이 비워졌고 이후 정상 시스템 생성, 미리보기 PLAYING 및 2채널 미터링을 확인했다.
- Stairwell 실제 Play에서 BGM PLAYING, FMOD RESULT.OK를 확인했다. 재시작 후 검증 로그에 ERR_MEMORY, ERR_INVALID_HANDLE, ERR_INVALID_PARAM, fmod_editor.log 파일 열기 경고가 없었다.
- 테스트 코드·전용 테스트 씬은 저장하지 않았다. 초기 자원 고갈까지 어떤 호출이 몇 개의 시스템을 남겼는지는 미확인이다.

#### 근거

- [FMOD 공식 답변: 시스템 수 제한과 ERR_MEMORY](https://qa.fmod.com/t/err-memory-not-enough-memory-or-resources/11816): FMOD 시스템 객체를 해제하지 않으면 생성 한도에 도달할 수 있다.
