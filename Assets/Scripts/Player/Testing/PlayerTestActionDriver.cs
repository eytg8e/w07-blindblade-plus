using UnityEngine;

/// <summary>
/// Drives impact and action-end callbacks with simple scaled-time timings for sandbox play.
/// Replace with animation events when _player animations are available.
/// </summary>
public sealed class PlayerTestActionDriver : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerAnimationEventRelay _animationEventRelay;
    [SerializeField, Min(0f)] private float _normalHitTime = 0.15f;
    [SerializeField, Min(0f)] private float _normalActionDuration = 0.4f;
    [SerializeField, Min(0f)] private float _iaidoHitTime = 0.18f;
    [SerializeField, Min(0f)] private float _iaidoSlashDuration = 0.5f;

    private PlayerActionState _activeState;
    private int _activeActionId;
    private float _elapsed;
    private bool _impactSent;
    private bool _endSent;

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
        }

        if (_endSent)
            return;

        _elapsed += Time.deltaTime;
        float hitTime = state == PlayerActionState.NormalAttack ? _normalHitTime : _iaidoHitTime;
        float actionDuration = state == PlayerActionState.NormalAttack ? _normalActionDuration : _iaidoSlashDuration;

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

    private void OnValidate()
    {
        _normalHitTime = Mathf.Min(_normalHitTime, _normalActionDuration);
        _iaidoHitTime = Mathf.Min(_iaidoHitTime, _iaidoSlashDuration);
    }
}