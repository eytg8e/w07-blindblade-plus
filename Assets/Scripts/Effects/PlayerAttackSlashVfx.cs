using UnityEngine;

/// <summary>
/// 플레이어 공격 시작에 자식 검기 파티클을 재생하고 완료 후 비활성화한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAttackSlashVfx : MonoBehaviour
{
    [Header("공격 검기 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private ParticleSystem _slashVfx;

    void Awake()
    {
        StopSlash();
    }

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
        StopSlash();
    }

    void Update()
    {
        if (_slashVfx.gameObject.activeSelf && !_slashVfx.IsAlive(true))
            _slashVfx.gameObject.SetActive(false);
    }

    /// <summary>
    /// action의 일반 공격 또는 발도 베기 시작이면 연결된 검기를 처음부터 재생한다.
    /// 사망 상태로 변경되면 파티클을 지우고 검기를 비활성화한다.
    /// </summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if ((action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted)
        {
            _slashVfx.gameObject.SetActive(true);
            _slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _slashVfx.Play(true);
        }
        else if (action.Stage == PlayerActionStage.StateChanged && action.State == PlayerActionState.Dead)
            StopSlash();
    }

    /// <summary>
    /// 연결된 _slashVfx와 자식 파티클의 방출 및 잔상을 즉시 정리한다.
    /// 검기 GameObject를 비활성 상태로 변경한다.
    /// </summary>
    private void StopSlash()
    {
        _slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _slashVfx.gameObject.SetActive(false);
    }
}
