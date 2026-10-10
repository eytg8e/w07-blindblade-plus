using UnityEngine;

/// <summary>
/// 게임 세션 관련 매니저와 공통 감각·전투 효과 표현 담당자를 보유한다.
/// </summary>
public class Game : MonoSingleton<Game>
{
    [Header("현재 플레이어")]
    private PlayerController _player;
    public static PlayerController Player => CurrentInstance != null ? CurrentInstance._player : null;

    [Header("게임 세션 매니저")]
    private SensoryManager _sensoryManager;
    private EffectManager _effectManager;
    public SensoryManager SensoryManager => _sensoryManager;
    public EffectManager EffectManager => _effectManager;
    public InputManager InputManager { get; private set; }
    public GameStateManager GameStateManager { get; private set; }
    public UIManager UIManager { get; private set; }

    /// <summary>등록된 플레이어로 공통 세션을 초기화하고 필수 매니저 준비 여부를 반환한다.</summary>
    public bool InitializeSession()
    {
        if (_player == null || InputManager == null || GameStateManager == null || UIManager == null)
        {
            Debug.LogError("게임 세션의 플레이어 또는 공통 매니저가 준비되지 않았습니다.", this);
            return false;
        }
        GameStateManager.Initialize(_player, _player.gameObject.scene.path);
        InputManager.Initialize(GameStateManager);
        UIManager.Initialize();
        
        if (UIManager.Get<GameOverUI>() != null) 
            return true;
        
        ShutdownSession();
        return false;
    }

    /// <summary>입력·화면·상태의 씬 연결을 해제하고 매니저 인스턴스는 유지한다.</summary>
    public void ShutdownSession()
    {
        if (InputManager != null) InputManager.Shutdown();
        if (UIManager != null) UIManager.Shutdown();
        if (GameStateManager != null) GameStateManager.Shutdown();
    }

    /// <summary>player를 현재 플레이어로 등록한다. 기존 생존 플레이어가 있으면 중복 등록을 거부한다.</summary>
    public void RegisterPlayer(PlayerController player)
    {
        if (_player != null && _player != player)
        {
            Debug.LogError("현재 플레이어가 이미 등록되어 있어 다른 플레이어를 등록할 수 없습니다.", player);
            return;
        }
        _player = player;
    }

    /// <summary>player가 현재 등록된 플레이어일 때만 참조를 해제한다. 종료 중 매니저를 생성하지 않는다.</summary>
    public static void UnregisterPlayer(PlayerController player)
    {
        if (CurrentInstance != null && CurrentInstance._player == player)
        {
            CurrentInstance._player = null;
        }
    }

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
        InputManager = LoadManager<InputManager>(ResourcePaths.INPUT_MANAGER_PATH);
        GameStateManager = LoadManager<GameStateManager>(ResourcePaths.GAME_STATE_MANAGER_PATH);
        UIManager = LoadManager<UIManager>(ResourcePaths.UI_MANAGER_PATH);
    }

    /// <summary>기존 T 매니저를 재사용하거나 path의 프리팹을 동적 생성해 반환한다.</summary>
    private T LoadManager<T>(string path) where T : MonoBehaviour
    {
        T manager = GetComponentInChildren<T>(true);
        if (manager != null) return manager;
        GameObject instance = Managers.Resource.Instantiate(path, transform, usePool: false);
        if (instance == null) return null;
        manager = instance.GetComponent<T>();
        if (manager == null)
        {
            Debug.LogError($"매니저 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다: {path}", instance);
            Managers.Resource.Destroy(instance);
        }
        return manager;
    }

}
