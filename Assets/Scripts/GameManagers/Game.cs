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

    /// <summary>싱글턴을 등록하고 기존 감각·이펙트 자식을 재사용하여 재컴파일 뒤 세션 서비스를 복원한다.</summary>
    protected override void InitializeSingleton()
    {
        base.InitializeSingleton();
        if (CurrentInstance != this)
            return;
        
        if (_sensoryManager == null)
            _sensoryManager = GetComponentInChildren<SensoryManager>(true);
        if (_sensoryManager == null)
        {
            var sensoryManagerGo = Managers.Resource.Instantiate(ResourcePaths.SENSORY_MANAGER_PATH, transform, usePool: false);
            _sensoryManager = sensoryManagerGo.GetComponent<SensoryManager>();
        }
        _sensoryManager.Initialize();
        if (_effectManager == null)
            _effectManager = GetComponentInChildren<EffectManager>(true);
        if (_effectManager == null)
        {
            var effectManagerGo = Managers.Resource.Instantiate(ResourcePaths.EFFECT_MANAGER_PATH, transform, usePool: false);
            _effectManager = effectManagerGo.GetComponent<EffectManager>();
        }
        _effectManager.Initialize();
    }

    void OnEnable()
    {
        if (Application.isPlaying)
            InitializeSingleton();
    }
}
