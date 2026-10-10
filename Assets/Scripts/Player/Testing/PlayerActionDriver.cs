using UnityEngine;

/// <summary>
/// Drives impact and action-end callbacks with simple scaled-time timings for sandbox play.
/// Replace with animation events when _player animations are available.
/// </summary>
public sealed class PlayerActionDriver : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerAnimationEventRelay _animationEventRelay;
    [SerializeField, Min(0f)] private float _normalHitTime = 0.15f;
    [SerializeField, Min(0f)] private float _normalActionDuration = 0.4f;
    [SerializeField, Min(0f)] private float _iaidoHitTime = 0.42f;
    [SerializeField, Min(0f)] private float _iaidoSlashDuration = 0.72f;

    private PlayerActionState _activeState;
    private int _activeActionId;
    private float _elapsed;
    private bool _impactSent;
    private bool _endSent;
    private bool _useIaidoTimingForNextNormalAttack;
    private bool _activeNormalAttackUsesIaidoTiming;

    private void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_animationEventRelay == null)
            _animationEventRelay = GetComponent<PlayerAnimationEventRelay>();
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
                && _useIaidoTimingForNextNormalAttack;
            if (state == PlayerActionState.NormalAttack)
                _useIaidoTimingForNextNormalAttack = false;
        }

        if (_endSent)
            return;

        _elapsed += Time.deltaTime;
        bool iaidoTiming = state == PlayerActionState.IaidoSlash
            || state == PlayerActionState.NormalAttack && _activeNormalAttackUsesIaidoTiming;
        float hitTime = iaidoTiming ? _iaidoHitTime : _normalHitTime;
        float actionDuration = iaidoTiming ? _iaidoSlashDuration : _normalActionDuration;

        if (!_impactSent && _elapsed >= hitTime)
        {
            _impactSent = true;
            if (state == PlayerActionState.NormalAttack)
            {
                if (_animationEventRelay != null)
                    _animationEventRelay.NormalAttackImpactEvent();
                else
                    _player.NotifyNormalAttackImpact(_activeActionId);
            }
            else
            {
                if (_animationEventRelay != null)
                    _animationEventRelay.IaidoSlashImpactEvent();
                else
                    _player.NotifyIaidoSlashImpact(_activeActionId);
            }
        }

        if (_elapsed < actionDuration)
            return;

        _endSent = true;
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
        bool iaido = kind == PlayerAttackKind.IaidoFinisher;
        impactTime = iaido ? _iaidoHitTime : _normalHitTime;
        duration = iaido ? _iaidoSlashDuration : _normalActionDuration;
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

    private void OnValidate()
    {
        _normalHitTime = Mathf.Min(_normalHitTime, _normalActionDuration);
        _iaidoHitTime = Mathf.Min(_iaidoHitTime, _iaidoSlashDuration);
    }
}
