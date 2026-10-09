using UnityEngine;

/// <summary>
/// 메인맵의 플레이어를 공통 감각 관측자로 연결하고 이동 발소리를 전달한다.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class MainMapSession : MonoBehaviour
{
    [Header("플레이어 연결")]
    [SerializeField] private PlayerController _player;

    void Start()
    {
        Game.Instance.SensoryManager.SetObserver(_player.transform);
    }

    void OnEnable()
    {
        _player.FootstepEmitted += HandlePlayerFootstep;
    }

    void OnDisable()
    {
        if (_player != null)
            _player.FootstepEmitted -= HandlePlayerFootstep;
    }

    /// <summary>
    /// step의 지면 위치와 소음 강도를 사용하여 기존 Footstep 소리를 한 번 요청한다.
    /// 재생 중 위치는 플레이어를 따라가며 시각 효과는 이 메서드에서 생성하지 않는다.
    /// </summary>
    private void HandlePlayerFootstep(PlayerFootstepEvent step)
    {
        Managers.Sound.PlayAttached(SoundId.Footstep, _player.gameObject, step.NoiseStrength);
    }
}
