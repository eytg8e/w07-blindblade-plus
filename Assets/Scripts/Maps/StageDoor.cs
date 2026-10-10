using UnityEngine;

using UnityEngine.InputSystem;

using BlindBlade.Enemies;

/// <summary>근처 플레이어의 문 열기 입력과 다음 씬으로 이동하는 통과 영역을 처리한다.</summary>
public sealed class StageDoor : MonoBehaviour
{
    [Header("문 연결")]
    [SerializeField] private Transform _hinge;
    [SerializeField] private BoxCollider _passage;
    [SerializeField] private EnemyHealth[] _requiredEnemies = System.Array.Empty<EnemyHealth>();
    private PlayerController _player;
    private OpenDoorPromptUI _promptUI;

    [Header("이동 설정")]
    [SerializeField] private string _nextScene;
    [SerializeField, Min(0.5f)] private float _interactionDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float _openingDuration = 0.6f;
    [SerializeField] private float _openAngle = -100f;
    private Quaternion _closedRotation;
    private float _openedAt;
    private bool _opened;
    private bool _loading;
    public bool IsOpen => _opened;
    public bool IsUnlocked
    {
        get
        {
            foreach (EnemyHealth enemy in _requiredEnemies)
                if (enemy != null && enemy.IsAlive)
                    return false;
            return true;
        }
    }

    void Awake()
    {
        _closedRotation = _hinge.localRotation;
    }

    void Start()
    {
        _player = Game.Player;
        if (_player == null)
        {
            Debug.LogError("문에 연결할 현재 플레이어가 등록되지 않았습니다.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (_loading || _player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
        {
            UpdatePrompt(false);
            return;
        }
        if (_opened)
        {
            float progress = Mathf.Clamp01((Time.time - _openedAt) / _openingDuration);
            _hinge.localRotation = Quaternion.Slerp(_closedRotation,
                _closedRotation * Quaternion.Euler(0f, _openAngle, 0f), progress);
            UpdatePrompt(false);
            if (progress >= 1f && !string.IsNullOrEmpty(_nextScene)
                && _passage.bounds.Contains(_player.transform.position + Vector3.up * 0.5f))
            {
                _loading = true;
                Managers.Sound.SetFootstepOnlyOwner(null);
                Managers.Scene.LoadSceneAsync(_nextScene);
            }
            return;
        }
        bool near = Vector3.Distance(_player.transform.position, transform.position) <= _interactionDistance;
        UpdatePrompt(near);
        if (!near)
            return;
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            TryOpen();
    }

    void OnDisable()
    {
        UpdatePrompt(false);
    }

    /// <summary>visible이면 UIManager에서 문 안내를 확보해 상태를 전달하고 아니면 자신의 안내를 숨긴다.</summary>
    private void UpdatePrompt(bool visible)
    {
        if (!visible)
        {
            if (_promptUI != null) _promptUI.HideFor(this);
            return;
        }
        if (_promptUI == null)
        {
            _promptUI = Game.Instance.UIManager.Get<OpenDoorPromptUI>();
            if (_promptUI == null)
            {
                enabled = false;
                return;
            }
        }
        _promptUI.ShowFor(this, IsUnlocked);
    }

    /// <summary>살아 있는 필수 적이 없고 플레이어가 문 근처에 있으면 개방을 시작하고 true를 반환한다.</summary>
    public bool TryOpen()
    {
        if (_player == null || _opened || _loading || !IsUnlocked || _player.IsPaused
            || _player.CurrentState == PlayerActionState.Dead
            || Vector3.Distance(_player.transform.position, transform.position) > _interactionDistance)
            return false;
        _opened = true;
        _openedAt = Time.time;
        UpdatePrompt(false);
        return true;
    }
}
