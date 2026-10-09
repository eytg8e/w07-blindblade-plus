using UnityEngine;

/// <summary>
/// 게임 세션 관련 매니저와 공통 감각·전투 효과 표현 담당자를 보유한다.
/// </summary>
public class Game : MonoSingleton<Game>
{
    [Header("게임 세션 매니저")]
    private SensoryManager _sensoryManager;
    private EffectManager _effectManager;
    public SensoryManager SensoryManager => _sensoryManager;
    public EffectManager EffectManager => _effectManager;

    protected override void Awake()
    {
        base.Awake();
        if (CurrentInstance != this)
            return;
        
        var sensoryManagerGo = Managers.Resource.Instantiate(ResourcePaths.SENSORY_MANAGER_PATH, transform, usePool: false);
        _sensoryManager = sensoryManagerGo.GetComponent<SensoryManager>();
        _sensoryManager.Initialize();
        var effectManagerGo = Managers.Resource.Instantiate(ResourcePaths.EFFECT_MANAGER_PATH, transform, usePool: false);
        _effectManager = effectManagerGo.GetComponent<EffectManager>();
        _effectManager.Initialize();
    }
}
