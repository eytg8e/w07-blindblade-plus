using UnityEngine;

/// <summary>
/// 게임 세션 관련 매니저와 공통 감각 표현 담당자를 보유한다.
/// </summary>
public class Game : MonoSingleton<Game>
{
    private const string SENSORY_MANAGER_PATH = "GameManagers/SensoryManager";

    private SensoryManager _sensoryManager;
    public SensoryManager SensoryManager => _sensoryManager;

    protected override void Awake()
    {
        base.Awake();
        if (CurrentInstance != this)
            return;
        GameObject instance = Managers.Resource.Instantiate(SENSORY_MANAGER_PATH, transform, usePool: false);
        if (instance == null)
            return;
        _sensoryManager = instance.GetComponent<SensoryManager>();
        _sensoryManager.Initialize();
    }
}
