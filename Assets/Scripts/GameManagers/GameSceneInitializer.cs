using UnityEngine;

/// <summary>프리팹의 씬 서비스를 연결하고 공통 세션 초기화 순서를 제어한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MainMapSession))]
[DefaultExecutionOrder(100)]
public sealed class GameSceneInitializer : MonoBehaviour
{
    [Header("공통 구성")]
    [SerializeField] private TerrainMemory _terrainMemoryPrefab;

    [Header("맵별 설정")]
    [SerializeField] private bool _useTerrainMemory;
    [SerializeField] private Vector2 _memoryMinimum = new Vector2(-24f, -6f);
    [SerializeField] private Vector2 _memorySize = new Vector2(48f, 76f);
    [SerializeField] private bool _showGameplayControls;

    [Header("검 소음")]
    [SerializeField] private bool _useSwordNoise;
    [SerializeField, Min(1f)] private float _swordHearingDistance = 40f;
    [SerializeField, Min(0.1f)] private float _swordAlertDuration = 15f;

    private MainMapSession _mapSession;
    private GameStateManager _state;
    private TerrainMemory _memory;
    private bool _started;

    void Start()
    {
        _started = true;
        Initialize();
    }

    void OnEnable()
    {
        if (_started) Initialize();
    }

    /// <summary>
    /// Game.Player의 전투 서비스를 연결하고 매니저·맵 세션·선택적 기억 프리팹을 순서대로 준비한다.
    /// 모든 준비가 끝난 뒤 Playing으로 전환하여 입력을 허용한다.
    /// </summary>
    private void Initialize()
    {
        if (_state != null) return;
        PlayerController player = Game.Player;
        if (player == null || (_useTerrainMemory && _terrainMemoryPrefab == null))
        {
            Debug.LogError("게임 씬의 Player 또는 TerrainMemory 프리팹 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }
        EnemyCombatService combat = player.GetComponent<EnemyCombatService>();
        if (combat == null)
        {
            Debug.LogError("Player 프리팹에 EnemyCombatService가 없습니다.", player);
            enabled = false;
            return;
        }
        combat.ConfigurePlayer(player.transform);
        player.ConfigureCombatService(combat);
        if (!Game.Instance.InitializeSession())
        {
            enabled = false;
            return;
        }
        _state = Game.Instance.GameStateManager;
        _mapSession = GetComponent<MainMapSession>();
        _mapSession.Initialize(_useSwordNoise, _swordHearingDistance, _swordAlertDuration);
        if (_useTerrainMemory)
        {
            _memory = Instantiate(_terrainMemoryPrefab, transform);
            _memory.enabled = true;
            _memory.Initialize(player.transform, _memoryMinimum, _memorySize);
        }
        if (_showGameplayControls) Game.Instance.UIManager.Show<GameplayControlsUI>();
        _state.BeginPlaying();
    }

    void OnDisable()
    {
        if (_mapSession != null) _mapSession.Shutdown();
        if (_state != null && Game.IsInitialized && !Game.IsQuitting) Game.Instance.ShutdownSession();
        if (_memory != null)
        {
            _memory.gameObject.SetActive(false);
            Destroy(_memory.gameObject);
        }
        _memory = null;
        _state = null;
    }
}
