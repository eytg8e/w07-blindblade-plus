using UnityEngine;
using UnityEngine.SceneManagement;

using BlindBlade.Combat;
using BlindBlade.Enemies;
using BlindBlade.Player.Katana;

/// <summary>복도에서 첫 적중 전 피격을 패배로 처리하고 재시작 UI를 연결한다.</summary>
public sealed class CorridorRoundController : MonoBehaviour
{
    [Header("전투 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private DamageReceiver _damageReceiver;
    [SerializeField] private PlayerInputCommandAdapter _input;
    private KatanaInputCommandAdapter _katanaInput;

    [Header("GameOver UI")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private UnityEngine.UI.Button _restartButton;
    [SerializeField] private UnityEngine.UI.Button _stayButton;
    private bool _hasHitEnemy;
    private bool _gameOver;
    private bool _restarting;
    private float _previousTimeScale;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;

    public bool IsGameOver => _gameOver;

    void Awake()
    {
        _katanaInput = _player.GetComponent<KatanaInputCommandAdapter>();
        _panel.SetActive(false);
    }

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
        _damageReceiver.DamageReceived += HandleDamage;
        _restartButton.onClick.AddListener(Restart);
        _stayButton.onClick.AddListener(Stay);
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
        _damageReceiver.DamageReceived -= HandleDamage;
        _restartButton.onClick.RemoveListener(Restart);
        _stayButton.onClick.RemoveListener(Stay);
        if (_gameOver)
        {
            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }
    }

    /// <summary>action의 적중 여부를 기록하며 일반 체력 사망도 GameOver로 전달한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if (action.Stage == PlayerActionStage.HitConfirmed)
            _hasHitEnemy = true;
        if (action.State == PlayerActionState.Dead)
            ShowGameOver();
    }

    /// <summary>context가 적의 실제 피해이고 플레이어가 먼저 명중하지 않았다면 패배를 확정한다.</summary>
    private void HandleDamage(DamageContext context)
    {
        if (!_hasHitEnemy && context.Instigator != null
            && context.Instigator.GetComponentInParent<EnemyHealth>() != null)
            ShowGameOver();
    }

    /// <summary>게임 진행과 입력을 정지하고 다시 시작 선택 화면을 한 번 표시한다.</summary>
    private void ShowGameOver()
    {
        if (_gameOver)
            return;
        _gameOver = true;
        _previousTimeScale = Time.timeScale;
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _input.enabled = false;
        if (_katanaInput != null)
            _katanaInput.enabled = false;
        _player.SetDead();
        Managers.Sound.SetFootstepOnlyOwner(null);
        Managers.Sound.StopEffects();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _panel.SetActive(true);
        _restartButton.Select();
    }

    /// <summary>패배 상태의 현재 씬을 다시 로드하여 캐릭터와 첫 적중 기록을 초기화한다.</summary>
    public void Restart()
    {
        if (!_gameOver || _restarting)
            return;
        _restarting = true;
        Time.timeScale = _previousTimeScale;
        string path = gameObject.scene.path;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(path);
#endif
    }

    /// <summary>재시작하지 않는 선택은 패배 화면에 머물러 나중에 다시 시작할 수 있도록 한다.</summary>
    private void Stay()
    {
        _restartButton.Select();
    }
}
