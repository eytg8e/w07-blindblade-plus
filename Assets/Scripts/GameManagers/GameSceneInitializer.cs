using UnityEngine;

using BlindBlade.Enemies;

/// <summary>씬 공통 구성을 생성하고 참조 연결 및 초기화 순서를 제어한다.</summary>
[DefaultExecutionOrder(100)]
public sealed class GameSceneInitializer : MonoBehaviour
{
    [Header("공통 구성")]
    [SerializeField] private TerrainMemory _terrainMemoryPrefab;

    [Header("맵별 설정")]
    [SerializeField] private bool _useTerrainMemory;
    [SerializeField] private Vector2 _memoryMinimum = new Vector2(-24f, -6f);
    [SerializeField] private Vector2 _memorySize = new Vector2(48f, 76f);
    [SerializeField] private bool _useCorridorRule;
    [SerializeField] private bool _showGameplayControls;
    private GameStateManager _state;
    private UIManager _ui;
    private CorridorRoundController _round;
    private TerrainMemory _memory;
    private PlayerController _player;
    private PlayerInputCommandAdapter _legacyInput;
    private BlindBlade.Player.Katana.KatanaInputCommandAdapter _katanaInput;
    private bool _started;

    void Start()
    {
        _started = true;
        Initialize();
    }

    void OnEnable()
    {
        if (_started) 
            Initialize();
    }

    /// <summary>미전환 씬의 round 규칙과 memory 범위를 런타임 구성에 전달한다.</summary>
    public void ConfigureLegacy(CorridorRoundController round, TerrainMemory memory)
    {
        _useCorridorRule = round != null;
        if (round != null) round.enabled = false;
        _useTerrainMemory = memory != null;
        if (memory != null)
        {
            _memoryMinimum = memory.Minimum;
            _memorySize = memory.Size;
            _terrainMemoryPrefab = memory;
            memory.enabled = false;
        }
    }

    /// <summary>현재 플레이어와 공통 구성을 준비하고 초기화가 끝나면 입력을 허용한다.</summary>
    private void Initialize()
    {
        if (_state != null) return;
        _player = Game.Player;
        if (_player == null
            || (_useTerrainMemory && _terrainMemoryPrefab == null))
        {
            Debug.LogError("게임 씬의 플레이어 또는 공통 구성 프리팹 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }
        if (!Game.Instance.InitializeSession())
        {
            enabled = false;
            return;
        }
        _state = Game.Instance.GameStateManager;
        _ui = Game.Instance.UIManager;
        EnemyCombatService combat = _player.GetComponent<EnemyCombatService>();
        if (combat == null)
        {
            Debug.LogError("플레이어 프리팹에 EnemyCombatService가 없습니다.", _player);
            enabled = false;
            return;
        }
        combat.ConfigurePlayer(_player.transform);
        _player.ConfigureCombatService(combat);
        _legacyInput = _player.GetComponent<PlayerInputCommandAdapter>();
        _katanaInput = _player.GetComponent<BlindBlade.Player.Katana.KatanaInputCommandAdapter>();
        if (_legacyInput != null && _legacyInput.isActiveAndEnabled) _legacyInput.Initialize();
        if (_katanaInput != null && _katanaInput.isActiveAndEnabled) _katanaInput.Initialize();
        if (_showGameplayControls) _ui.Show<GameplayControlsUI>();
        InitializeSensory();
        if (_useTerrainMemory)
        {
            _memory = Instantiate(_terrainMemoryPrefab, transform);
            _memory.enabled = true;
            _memory.Initialize(_player.transform, _memoryMinimum, _memorySize);
        }
        if (_useCorridorRule)
        {
            _round = gameObject.AddComponent<CorridorRoundController>();
            _round.Initialize(_state);
        }
        _state.BeginPlaying();
    }

    /// <summary>현재 플레이어를 감각 관측자로 연결하고 공통 표현과 발걸음 이벤트를 준비한다.</summary>
    private void InitializeSensory()
    {
        SensoryManager sensory = Game.Instance.SensoryManager;
        sensory.SetObserver(_player.transform);
        if (!sensory.IsReady) sensory.Initialize();
        sensory.GetComponent<SensoryPerceptionMask>().SetFootstepOnly(false);
        sensory.SetEffectsVisible(true);
        sensory.SetFootstepsSuppressed(false);
        Managers.Sound.SetFootstepOnlyOwner(null);
        _player.FootstepEmitted += HandlePlayerFootstep;
    }

    /// <summary>step의 소음 강도로 현재 플레이어에 붙은 발소리를 요청한다.</summary>
    private void HandlePlayerFootstep(PlayerFootstepEvent step)
    {
        Managers.Sound.PlayAttached(SoundId.Footstep, _player.gameObject, step.NoiseStrength);
    }

    void OnDisable()
    {
        if (_legacyInput != null) _legacyInput.Shutdown();
        if (_katanaInput != null) _katanaInput.Shutdown();
        if (_round != null) _round.Shutdown();
        if (_player != null) _player.FootstepEmitted -= HandlePlayerFootstep;
        if (Managers.IsInitialized) Managers.Sound.SetFootstepOnlyOwner(null);
        if (_state != null)
        {
            Game.Instance.ShutdownSession();
        }
        if (_memory != null) _memory.gameObject.SetActive(false);
        if (_memory != null) Destroy(_memory.gameObject);
        if (_round != null) Destroy(_round);
        _state = null;
        _ui = null;
    }
}
