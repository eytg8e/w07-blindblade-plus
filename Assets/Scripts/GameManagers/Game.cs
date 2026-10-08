using UnityEngine;

/// <summary>
/// 게임 세션 관련 매니저와 공통 감각 표현 담당자를 보유한다.
/// </summary>
public class Game : MonoSingleton<Game>
{
    private static SensoryManager _sensoryManager;
    public static SensoryManager SensoryManager => _sensoryManager;

    protected override void Awake()
    {
        base.Awake();
        if (CurrentInstance != this)
            return;
        
        var sensoryManagerGo = Managers.Resource.Instantiate(ResourcePaths.SENSORY_MANAGER_PATH, transform, usePool: false);
        _sensoryManager = sensoryManagerGo.GetComponent<SensoryManager>();
        _sensoryManager.Initialize();
    }
}
