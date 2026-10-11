using UnityEngine;

/// <summary>
/// 공격의 준비·베기·종료 시간을 관리하고 Animator 평가 뒤 실제 칼날 피해 검사를 요청한다.
/// 표현 담당자에게 같은 베기 구간을 제공한다.
/// </summary>
public sealed class PlayerActionDriver : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerAnimationEventRelay _animationEventRelay;
    [SerializeField, Min(0f)] private float _normalHitTime = 0.15f;
    [SerializeField, Min(0f)] private float _normalActionDuration = 0.4f;
    [SerializeField, Min(0f)] private float _iaidoHitTime = 0.42f;
    [SerializeField, Min(0f)] private float _iaidoSlashDuration = 0.72f;
    [Header("대상 돌진 연계 타이밍")]
    [SerializeField, Min(0.01f)] private float _targetedComboHitTime = 0.12f;
    [SerializeField, Min(0.01f)] private float _targetedComboActionDuration = 0.32f;
    [Header("베기 판정 구간")]
    [SerializeField, Min(0.01f)] private float _normalStrikeWindow = 0.16f;
    [SerializeField, Min(0.01f)] private float _strikeWindow = 0.22f;

    [Header("패링 반격 타이밍")]
    [SerializeField, Min(0.01f)] private float _counterHitTime = 0.12f;
    [SerializeField, Min(0.01f)] private float _counterActionDuration = 0.34f;
    [SerializeField, Min(0.01f)] private float _counterStrikeWindow = 0.12f;
    private KatanaCombatCoordinator _combat;
    private KatanaWeaponVisual _weaponVisual;
    private bool _activeNormalAttackIsCounter;

    private PlayerActionState _activeState;
    private int _activeActionId;
    private float _elapsed;
    private bool _impactSent;
    private bool _endSent;
    private bool _useIaidoTimingForNextNormalAttack;
    private bool _activeNormalAttackUsesIaidoTiming;
    private bool _sweepPending;
    private bool _sweepFinalSample;
    private bool _endPending;

    private void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_animationEventRelay == null)
            _animationEventRelay = GetComponent<PlayerAnimationEventRelay>();
        _combat = GetComponent<KatanaCombatCoordinator>();
        _weaponVisual = GetComponent<KatanaWeaponVisual>();
    }

    private void Update()
    {
        if (_player == null || _player.IsPaused)
            return;

        PlayerActionState state = _player.CurrentState;
        if (state != PlayerActionState.NormalAttack && state != PlayerActionState.IaidoSlash)
        {
            _activeState = state;
            _activeActionId = _player.CurrentActionId;
            _elapsed = 0f;
            _impactSent = false;
            _endSent = false;
            _sweepPending = false;
            _endPending = false;
            return;
        }

        if (state != _activeState || _player.CurrentActionId != _activeActionId)
        {
            _activeState = state;
            _activeActionId = _player.CurrentActionId;
            _elapsed = 0f;
            _impactSent = false;
            _endSent = false;
            _activeNormalAttackUsesIaidoTiming = state == PlayerActionState.NormalAttack
                && (_useIaidoTimingForNextNormalAttack || (_combat != null && _combat.IsOpeningAttack) || UsesOpeningTimingForSecondAttack());
            _activeNormalAttackIsCounter = state == PlayerActionState.NormalAttack
                && _combat != null && _combat.IsCounterAttack;
            if (state == PlayerActionState.NormalAttack)
                _useIaidoTimingForNextNormalAttack = false;
        }

        if (_endSent)
            return;

        _elapsed += Time.deltaTime;
        bool targetedComboAttack = state == PlayerActionState.IaidoSlash
            && _player.TargetedComboStrike > 0;
        bool iaidoTiming = state == PlayerActionState.IaidoSlash && !targetedComboAttack
            || state == PlayerActionState.NormalAttack && _activeNormalAttackUsesIaidoTiming;
        float hitTime = _activeNormalAttackIsCounter ? _counterHitTime
            : targetedComboAttack ? _targetedComboHitTime : iaidoTiming ? _iaidoHitTime : _normalHitTime;
        float actionDuration = _activeNormalAttackIsCounter ? _counterActionDuration
            : targetedComboAttack ? _targetedComboActionDuration : iaidoTiming ? _iaidoSlashDuration : _normalActionDuration;

        float strikeWindow = _activeNormalAttackIsCounter ? _counterStrikeWindow
            : iaidoTiming ? _strikeWindow : _normalStrikeWindow;
        float strikeStart = Mathf.Max(0f, hitTime - strikeWindow * 0.5f);
        float strikeEnd = Mathf.Min(actionDuration, hitTime + strikeWindow * 0.5f);
        if (state == PlayerActionState.NormalAttack && !_impactSent && _elapsed >= strikeStart)
        {
            bool finalSample = _elapsed >= strikeEnd;
            _sweepPending = true;
            _sweepFinalSample = finalSample;
            _impactSent = finalSample;
        }

        if (state == PlayerActionState.IaidoSlash && !_impactSent && _elapsed >= hitTime)
        {
            _impactSent = true;
            if (_animationEventRelay != null) _animationEventRelay.IaidoSlashImpactEvent();
            else _player.NotifyIaidoSlashImpact(_activeActionId);
        }

        if (_elapsed < actionDuration)
            return;

        _endSent = true;
        if (state == PlayerActionState.NormalAttack && _sweepPending)
        {
            _endPending = true;
            return;
        }
        if (state == PlayerActionState.NormalAttack)
        {
            if (_animationEventRelay != null)
                _animationEventRelay.NormalAttackEndedEvent();
            else
                _player.NotifyNormalAttackAnimationEnded(_activeActionId);
        }
        else
        {
            if (_animationEventRelay != null)
                _animationEventRelay.IaidoSlashEndedEvent();
            else
                _player.NotifyIaidoSlashAnimationEnded(_activeActionId);
        }
    }

    /// <summary>kind의 실제 적중·종료 시간을 읽어 시각 모션에 제공한다. 드라이버의 판정 시간은 변경하지 않는다.</summary>
    public void GetPresentationTiming(PlayerAttackKind kind, out float impactTime, out float duration)
    {
        if (kind == PlayerAttackKind.Normal && _combat != null && _combat.IsCounterAttack)
        {
            impactTime = _counterHitTime;
            duration = _counterActionDuration;
            return;
        }
        bool targetedComboAttack = kind == PlayerAttackKind.IaidoFinisher
            && _player != null && _player.TargetedComboStrike > 0;
        bool iaido = kind == PlayerAttackKind.IaidoFinisher && !targetedComboAttack
            || kind == PlayerAttackKind.Normal && _combat != null && (_combat.IsOpeningAttack || UsesOpeningTimingForSecondAttack());
        impactTime = targetedComboAttack ? _targetedComboHitTime : iaido ? _iaidoHitTime : _normalHitTime;
        duration = targetedComboAttack ? _targetedComboActionDuration : iaido ? _iaidoSlashDuration : _normalActionDuration;
    }

    /// <summary>다음 일반 공격을 발도 모션에 맞춘 적중·종료 시간으로 처리하도록 예약한다.</summary>
    public void UseIaidoTimingForNextNormalAttack()
    {
        _useIaidoTimingForNextNormalAttack = true;
    }

    /// <summary>현재 actionId의 게임 시간 경과를 반환한다. 아직 시작되지 않았거나 다른 행동이면 0을 반환한다.</summary>
    public float GetPresentationElapsed(int actionId)
    {
        return actionId == _activeActionId ? _elapsed : 0f;
    }

    /// <summary>현재 일반 2타가 교체 클립이며 1타 타이밍 사용 설정이 켜졌는지 검사해 동일 타이밍 적용 여부를 반환한다.</summary>
    private bool UsesOpeningTimingForSecondAttack()
    {
        return _combat != null && _combat.Strike == 2 && !_combat.IsCounterAttack
            && _weaponVisual != null && _weaponVisual.UsesOpeningTimingForSecondAttack;
    }

    void LateUpdate()
    {
        if (!_sweepPending || _player.IsPaused) return;
        _sweepPending = false;
        _player.NotifyNormalAttackSweep(_activeActionId, _sweepFinalSample);
        if (_endPending)
        {
            _endPending = false;
            _player.NotifyNormalAttackAnimationEnded(_activeActionId);
        }
    }

    /// <summary>kind의 실제 타격 시간과 베기 폭으로 피해·접촉·검기 표현의 공통 시작·종료 시간을 반환한다.</summary>
    public void GetStrikeWindow(PlayerAttackKind kind, out float start, out float end)
    {
        GetPresentationTiming(kind, out float impact, out float duration);
        bool targetedComboAttack = kind == PlayerAttackKind.IaidoFinisher
            && _player != null && _player.TargetedComboStrike > 0;
        float window = kind == PlayerAttackKind.Normal || targetedComboAttack
            ? (_combat != null && _combat.IsCounterAttack ? _counterStrikeWindow
                : _combat != null && (_combat.IsOpeningAttack || kind == PlayerAttackKind.Normal && UsesOpeningTimingForSecondAttack())
                    ? _strikeWindow : _normalStrikeWindow) : _strikeWindow;
        start = Mathf.Max(0f, impact - window * 0.5f);
        end = Mathf.Min(duration, impact + window * 0.5f);
    }

    private void OnValidate()
    {
        _normalHitTime = Mathf.Min(_normalHitTime, _normalActionDuration);
        _iaidoHitTime = Mathf.Min(_iaidoHitTime, _iaidoSlashDuration);
        _targetedComboHitTime = Mathf.Min(_targetedComboHitTime, _targetedComboActionDuration);
        _counterHitTime = Mathf.Min(_counterHitTime, _counterActionDuration);
    }
}
