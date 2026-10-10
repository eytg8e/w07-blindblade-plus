using UnityEngine;
using UnityEngine.InputSystem;

using TMPro;

using BlindBlade.Enemies;

/// <summary>근처 플레이어의 문 열기 입력과 다음 씬으로 이동하는 통과 영역을 처리한다.</summary>
public sealed class StageDoor : MonoBehaviour
{
    [Header("문 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private Transform _hinge;
    [SerializeField] private BoxCollider _passage;
    [SerializeField] private TMP_Text _prompt;
    [SerializeField] private EnemyHealth[] _requiredEnemies = System.Array.Empty<EnemyHealth>();

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
        _prompt.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_loading || _player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
        {
            _prompt.gameObject.SetActive(false);
            return;
        }
        if (_opened)
        {
            float progress = Mathf.Clamp01((Time.time - _openedAt) / _openingDuration);
            _hinge.localRotation = Quaternion.Slerp(_closedRotation,
                _closedRotation * Quaternion.Euler(0f, _openAngle, 0f), progress);
            _prompt.gameObject.SetActive(false);
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
        _prompt.gameObject.SetActive(near);
        if (!near)
            return;
        _prompt.text = IsUnlocked ? "[F] OPEN DOOR" : "DEFEAT ALL ENEMIES";
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            TryOpen();
    }

    /// <summary>살아 있는 필수 적이 없고 플레이어가 문 근처에 있으면 개방을 시작하고 true를 반환한다.</summary>
    public bool TryOpen()
    {
        if (_opened || _loading || !IsUnlocked || _player.IsPaused
            || _player.CurrentState == PlayerActionState.Dead
            || Vector3.Distance(_player.transform.position, transform.position) > _interactionDistance)
            return false;
        _opened = true;
        _openedAt = Time.time;
        return true;
    }
}
