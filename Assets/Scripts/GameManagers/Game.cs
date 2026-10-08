using UnityEngine;

/// <summary>
/// 게임 세션 관련 매니저와 공통 감각 표현 담당자를 보유한다.
/// </summary>
public class Game : MonoSingleton<Game>
{
    [Header("감각 표현")]
    [SerializeField] private SensoryEmitter _emitter;
    public SensoryEmitter Emitter => _emitter;

    protected override void Awake()
    {
        base.Awake();
        if (CurrentInstance != this)
            return;
        if (_emitter == null || !_emitter.transform.IsChildOf(transform))
        {
            Debug.LogError("Game 자신 또는 자식에 배치한 SensoryEmitter를 연결해야 합니다.", this);
            return;
        }
        _emitter.Initialize();
    }
}
