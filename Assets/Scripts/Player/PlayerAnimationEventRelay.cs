using UnityEngine;

/// <summary>
/// Relays Unity Animation Events to the player action controller.
/// Attach this beside the Animator that owns the attack clips; assign the PlayerController
/// or leave it empty when the player controller is on this object or a parent.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationEventRelay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController _player;

    private int _activeActionId;
    private PlayerActionState _activeActionState;
    private bool _hasActiveAction;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void OnEnable()
    {
        ResolvePlayer();
        if (_player != null)
            _player.ActionEvent += HandleActionEvent;
    }

    private void OnDisable()
    {
        if (_player != null)
            _player.ActionEvent -= HandleActionEvent;

        ClearTrackedAction();
    }

    /// <summary>Animation Event at the normal attack's impact frame.</summary>
    public void NormalAttackImpactEvent()
    {
        if (IsCurrentAction(PlayerActionState.NormalAttack))
            _player.NotifyNormalAttackImpact(_activeActionId);
    }

    /// <summary>Animation Event at the end of the normal attack clip.</summary>
    public void NormalAttackEndedEvent()
    {
        if (IsCurrentAction(PlayerActionState.NormalAttack))
            _player.NotifyNormalAttackAnimationEnded(_activeActionId);
    }

    /// <summary>Animation Event at the iaido slash's impact frame.</summary>
    public void IaidoSlashImpactEvent()
    {
        if (IsCurrentAction(PlayerActionState.IaidoSlash))
            _player.NotifyIaidoSlashImpact(_activeActionId);
    }

    /// <summary>Animation Event at the end of the iaido slash clip.</summary>
    public void IaidoSlashEndedEvent()
    {
        if (IsCurrentAction(PlayerActionState.IaidoSlash))
            _player.NotifyIaidoSlashAnimationEnded(_activeActionId);
    }

    private void HandleActionEvent(PlayerActionEvent actionEvent)
    {
        if (actionEvent.Stage == PlayerActionStage.AttackStarted
            && actionEvent.State == PlayerActionState.NormalAttack)
        {
            TrackAction(actionEvent.ActionId, PlayerActionState.NormalAttack);
            return;
        }

        if ((actionEvent.Stage == PlayerActionStage.DashStarted
                && actionEvent.State == PlayerActionState.IaidoDash)
            || (actionEvent.Stage == PlayerActionStage.SlashStarted
                && actionEvent.State == PlayerActionState.IaidoSlash))
        {
            TrackAction(actionEvent.ActionId, actionEvent.State);
            return;
        }

        if (_hasActiveAction && actionEvent.ActionId == _activeActionId
            && (actionEvent.Stage == PlayerActionStage.ActionEnded
                || actionEvent.Stage == PlayerActionStage.RequestRejected
                || actionEvent.State == PlayerActionState.Dead))
        {
            ClearTrackedAction();
        }
    }

    private bool IsCurrentAction(PlayerActionState expectedState)
    {
        return _player != null
            && _hasActiveAction
            && _activeActionState == expectedState
            && _player.CurrentState == expectedState
            && _player.CurrentActionId == _activeActionId;
    }

    private void TrackAction(int actionId, PlayerActionState state)
    {
        _activeActionId = actionId;
        _activeActionState = state;
        _hasActiveAction = true;
    }

    private void ClearTrackedAction()
    {
        _activeActionId = 0;
        _activeActionState = PlayerActionState.Free;
        _hasActiveAction = false;
    }

    private void ResolvePlayer()
    {
        if (_player == null)
            _player = GetComponentInParent<PlayerController>();
    }
}
