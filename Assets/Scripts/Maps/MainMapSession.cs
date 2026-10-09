using UnityEngine;

using BlindBlade.Rendering;

/// <summary>
/// 메인맵의 플레이어를 공통 감각 관측자로 연결하고 이동 발소리를 전달한다.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class MainMapSession : MonoBehaviour
{
    [Header("플레이어 연결")]
    [SerializeField] private PlayerController _player;

    [Header("이동 감각")]
    [SerializeField, Min(0f)] private float _movementGrace = 0.12f;
    private SensoryPerceptionMask _perceptionMask;
    private Vector3 _previousPosition;
    private float _lastMovementTime = float.NegativeInfinity;

    void Start()
    {
        Game.Instance.SensoryManager.SetObserver(_player.transform);
        _perceptionMask = Game.Instance.SensoryManager.GetComponent<SensoryPerceptionMask>();
        _perceptionMask.SetFootstepOnly(true);
        Game.Instance.SensoryManager.SetEffectsVisible(false);
        _previousPosition = _player.transform.position;
    }

    void Update()
    {
        Vector3 position = _player.transform.position;
        if (Vector3.ProjectOnPlane(position - _previousPosition, Vector3.up).sqrMagnitude > 0.000001f)
            _lastMovementTime = Time.time;
        _previousPosition = position;
        Managers.Sound.SetFootstepOnlyOwner(
            Time.time - _lastMovementTime <= _movementGrace ? _player.gameObject : null);
    }

    void OnEnable()
    {
        _player.FootstepEmitted += HandlePlayerFootstep;
        _previousPosition = _player.transform.position;
        _lastMovementTime = float.NegativeInfinity;
        if (_perceptionMask != null)
        {
            _perceptionMask.SetFootstepOnly(true);
            _perceptionMask.GetComponent<SensoryManager>().SetEffectsVisible(false);
        }
    }

    void OnDisable()
    {
        if (_player != null)
            _player.FootstepEmitted -= HandlePlayerFootstep;
        if (_perceptionMask != null)
        {
            _perceptionMask.SetFootstepOnly(false);
            _perceptionMask.GetComponent<SensoryManager>().SetEffectsVisible(true);
        }
        if (Managers.IsInitialized)
            Managers.Sound.SetFootstepOnlyOwner(null);
    }

    /// <summary>
    /// step의 지면 위치와 소음 강도를 사용하여 기존 Footstep 소리를 한 번 요청한다.
    /// 착지 위치 주변을 잠깐 표시하고 재생 중 소리는 플레이어를 따라간다.
    /// </summary>
    private void HandlePlayerFootstep(PlayerFootstepEvent step)
    {
        _lastMovementTime = Time.time;
        Managers.Sound.SetFootstepOnlyOwner(_player.gameObject);
        _perceptionMask.RevealFootstep(step.WorldPosition);
        Managers.Sound.PlayAttached(SoundId.Footstep, _player.gameObject, step.NoiseStrength);
    }
}
