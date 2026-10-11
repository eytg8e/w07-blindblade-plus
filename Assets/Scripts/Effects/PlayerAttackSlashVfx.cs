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
    [SerializeField, InspectorName("1타 VFX"), Tooltip("Player 프리팹 자식에 배치한 VFX의 루트 ParticleSystem을 연결합니다.")]
    private ParticleSystem _slashVfx;
    [SerializeField, InspectorName("2타 VFX")]
    private ParticleSystem _secondSlashVfx;
    [SerializeField, HideInInspector]
    private ParticleSystem _thirdSlashVfx;
    private PlayerActionDriver _timing;
    private KatanaCombatCoordinator _combat;
    private ParticleSystem _pendingSlashVfx;
    private ParticleSystem _currentSlashVfx;
    private int _pendingAction;
    private PlayerAttackKind _pendingKind;
    private bool _pending;

    void Awake()
    {
        _timing = GetComponent<PlayerActionDriver>();
        _combat = GetComponent<KatanaCombatCoordinator>();
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
        if (_pending && !_player.IsPaused)
        {
            if (_player.CurrentActionId != _pendingAction || (_player.CurrentState != PlayerActionState.NormalAttack
                && _player.CurrentState != PlayerActionState.IaidoSlash)) _pending = false;
            else
            {
                _timing.GetStrikeWindow(_pendingKind, out float start, out _);
                if (_timing.GetPresentationElapsed(_pendingAction) >= start)
                {
                    _pending = false;
                    _currentSlashVfx = _pendingSlashVfx;
                    _currentSlashVfx.gameObject.SetActive(true);
                    _currentSlashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _currentSlashVfx.Play(true);
                }
            }
        }
        if (_currentSlashVfx != null && _currentSlashVfx.gameObject.activeSelf && !_currentSlashVfx.IsAlive(true))
            _currentSlashVfx.gameObject.SetActive(false);
    }

    /// <summary>
    /// action의 공격 시작이면 공통 베기 구간에 검기 재생을 예약한다.
    /// 사망 상태로 변경되면 예약과 파티클을 지우고 검기를 비활성화한다.
    /// </summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if ((action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted)
        {
            StopSlash();
            _pendingAction = action.ActionId;
            _pendingKind = action.Stage == PlayerActionStage.SlashStarted ? PlayerAttackKind.IaidoFinisher : PlayerAttackKind.Normal;
            _pendingSlashVfx = GetSlashForAttack(_pendingKind);
            _pending = _pendingSlashVfx != null;
        }
        else if (action.Stage == PlayerActionStage.StateChanged && action.State == PlayerActionState.Dead)
            StopSlash();
    }

    /// <summary>
    /// kind와 현재 연계 타수로 연결된 자식 VFX를 반환한다.
    /// 충전 발도·패링 단발 반격은 기존 1타 VFX를 사용하며 빈 슬롯은 해당 공격의 검기를 생략한다.
    /// </summary>
    private ParticleSystem GetSlashForAttack(PlayerAttackKind kind)
    {
        if (kind == PlayerAttackKind.IaidoFinisher || _combat == null || _combat.IsCounterAttack)
            return _slashVfx;
        return _combat.Strike == 2 ? _secondSlashVfx : _slashVfx;
    }

    /// <summary>
    /// 세 타의 연결된 파티클 방출·잔상과 현재 예약 상태를 정리한다.
    /// 외부 입력 없이 각 VFX 자식을 비활성화하며 사용자가 설정한 Transform은 유지한다.
    /// </summary>
    private void StopSlash()
    {
        _pending = false;
        _pendingSlashVfx = null;
        _currentSlashVfx = null;
        StopParticle(_slashVfx);
        StopParticle(_secondSlashVfx);
        StopParticle(_thirdSlashVfx);
    }

    /// <summary>
    /// slash의 자식 파티클까지 정지하고 VFX GameObject를 비활성화한다.
    /// 비어 있는 Inspector 슬롯은 처리하지 않는다.
    /// </summary>
    private void StopParticle(ParticleSystem slash)
    {
        if (slash == null) return;
        slash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        slash.gameObject.SetActive(false);
    }
}
