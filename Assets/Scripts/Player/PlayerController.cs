using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Receives gameplay commands and coordinates player state, combat contracts, and motor behavior.
/// It has no direct dependency on Unity Input System.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private Camera _viewCamera;
    [SerializeField] private MonoBehaviour _perceptionProviderSource;
    [SerializeField] private MonoBehaviour _combatServiceSource;
    [SerializeField] private MonoBehaviour _mapServiceSource;

    [Header("Charge")]
    [SerializeField] private bool _enableChargeAttack;
    [SerializeField, Min(0f)] private float _minimumChargeTime = 1f;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float _normalAttackRange = 1.5f;
    [SerializeField, Min(0f)] private float _iaidoAttackRange = 2f;
    [SerializeField, Min(0f)] private float _targetStandOff = 0.15f;
    [SerializeField, Min(0f)] private float _attackOriginHeight = 1.1f;

    [Header("Recovery (scaled game time)")]
    [SerializeField, Min(0f)] private float _normalAttackRecovery = 0.25f;
    [SerializeField, Min(0f)] private float _guardStartRecovery = 0f;
    [SerializeField, Min(0f)] private float _guardEndRecovery = 0f;
    [SerializeField, Min(0f)] private float _guardSuccessRecovery = 0f;
    [SerializeField, Min(0f)] private float _chargeEarlyCancelRecovery = 0f;
    [SerializeField, Min(0f)] private float _chargeReleaseRecovery = 0f;
    [SerializeField, Min(0f)] private float _iaidoHitRecovery = 0.15f;
    [SerializeField, Min(0f)] private float _iaidoMissRecovery = 0.7f;
    [SerializeField, Min(0f)] private float _iaidoWallBlockedRecovery = 0.9f;

    private PlayerActionState _state = PlayerActionState.Free;
    private PlayerActionState _stateBeforePause = PlayerActionState.Free;
    private IPlayerPerceptionProvider _perceptionProvider;
    private IPlayerCombatService _combatService;
    private IPlayerMapService _mapService;
    private Vector2 _moveInput;
    private Vector2 _requestedMoveInput;
    private float _chargeElapsed;
    private float _recoveryRemaining;
    private bool _chargeReady;
    private bool _attackImpactResolved;
    private bool _attackActionFinished;
    private int _nextActionId;
    private int _currentActionId;
    private string _preferredTargetId;
    private Vector3 _lockedAttackDirection;
    private PlayerAttackOutcome _currentAttackOutcome;
    private PlayerSenseContext _lastSenseContext;
    private bool _hasSenseContext;

    public event Action<PlayerActionEvent> ActionEvent;
    public event Action<PlayerFootstepEvent> FootstepEmitted;
    public event Action<PlayerSenseContext> SenseContextChanged;

    public PlayerActionState CurrentState => _state;
    public bool IsGuarding => _state == PlayerActionState.Guarding;
    public bool IsCharging => _state == PlayerActionState.Charging;
    public bool IsChargingReady => _state == PlayerActionState.Charging && _chargeReady;
    /// <summary>실제 차징 시간을 0~1로 읽어 표현에 전달한다. 행동 타이밍은 변경하지 않는다.</summary>
    public float ChargeProgress => _minimumChargeTime <= 0f ? 1f : Mathf.Clamp01(_chargeElapsed / _minimumChargeTime);
    public bool IsPaused => _state == PlayerActionState.Paused;
    public int CurrentActionId => _currentActionId;
    public bool IsInRecovery => _state == PlayerActionState.Recovery;
    public float RecoveryRemaining => _recoveryRemaining;
    public bool IsCrouching => _motor != null && _motor.IsCrouching;

    private void Awake()
    {
        if (_motor == null)
            _motor = GetComponent<PlayerMotor>();
        if (_viewCamera == null)
            _viewCamera = GetComponentInChildren<Camera>();

        BindConfiguredServices();
    }

    private void OnEnable()
    {
        if (_motor == null)
            _motor = GetComponent<PlayerMotor>();

        if (_motor != null)
        {
            _motor.FootstepEmitted += ForwardFootstep;
            _motor.DashEnded += HandleDashEnded;
        }
    }

    private void OnDisable()
    {
        if (_motor != null)
        {
            _motor.FootstepEmitted -= ForwardFootstep;
            _motor.DashEnded -= HandleDashEnded;
        }
    }

    private void Update()
    {
        if (_state == PlayerActionState.Paused || _state == PlayerActionState.Dead)
            return;

        float deltaTime = Time.deltaTime;
        if (_state == PlayerActionState.Recovery)
        {
            _recoveryRemaining = Mathf.Max(0f, _recoveryRemaining - deltaTime);
            if (_recoveryRemaining <= 0f)
                EndRecovery();
        }
        else if (_state == PlayerActionState.Charging && !_chargeReady)
        {
            _chargeElapsed += deltaTime;
            if (_chargeElapsed >= _minimumChargeTime)
            {
                _chargeReady = true;
                Publish(PlayerActionStage.ChargeReady);
            }
        }

        PlayerLocomotionMode mode = GetLocomotionMode();
        bool movementLocked = IsMovementLocked();
        bool lookLocked = _state == PlayerActionState.Paused || _state == PlayerActionState.Dead;
        _motor?.Tick(mode, movementLocked, lookLocked, deltaTime);
        PublishSenseContext(mode);
    }

    /// <summary>감지, 전투, 맵 서비스 구현체를 등록합니다. 전달한 계약을 저장하고 전투·맵 서비스를 PlayerMotor에도 전달합니다.</summary>
    public void ConfigureServices(
        IPlayerPerceptionProvider perception,
        IPlayerCombatService combat,
        IPlayerMapService map)
    {
        _perceptionProvider = perception;
        _combatService = combat;
        _mapService = map;
        if (_motor != null)
        {
            _motor.ConfigureMapService(_mapService);
            _motor.ConfigureCombatService(_combatService);
        }
        _hasSenseContext = false;
    }

    /// <summary>
    /// 회복 중에는 입력 방향을 저장하고 실제 이동은 멈춘다.
    /// value를 사용하며, 회복 종료 시 마지막 이동 입력을 모터에 복원한다.
    /// </summary>
    public void SetMovementInput(Vector2 value)
    {
        if (_state == PlayerActionState.Paused
            || _state == PlayerActionState.Dead
            || _state == PlayerActionState.IaidoDash
            || _state == PlayerActionState.IaidoSlash)
        {
            _requestedMoveInput = Vector2.zero;
            _moveInput = Vector2.zero;
            _motor?.SetMoveInput(Vector2.zero);
            return;
        }

        _requestedMoveInput = Vector2.ClampMagnitude(value, 1f);
        if (_state == PlayerActionState.Recovery)
        {
            _moveInput = Vector2.zero;
            _motor?.SetMoveInput(Vector2.zero);
            return;
        }

        _moveInput = _requestedMoveInput;
        _motor?.SetMoveInput(_moveInput);
    }

    /// <summary>입력 어댑터가 계산한 웅크리기 상태를 이동 장치에 전달합니다. 일시정지나 사망 상태에서는 요청을 무시합니다.</summary>
    public void SetCrouchInput(bool isCrouching)
    {
        if (_state == PlayerActionState.Paused || _state == PlayerActionState.Dead)
            return;
        _motor?.SetCrouchInput(isCrouching);
    }

    /// <summary>마우스 또는 패드 시점 변화량을 이동 장치에 누적 전달합니다. 일시정지나 사망 상태에서는 입력을 무시합니다.</summary>
    public void AddLookInput(Vector2 delta)
    {
        if (_state == PlayerActionState.Paused || _state == PlayerActionState.Dead)
            return;
        _motor?.AddLookDelta(delta);
    }

    /// <summary>자유 상태에서 가드를 시작합니다. 상태가 허용되지 않으면 false를 반환합니다.</summary>
    public bool BeginGuard()
    {
        if (_state != PlayerActionState.Free)
            /// <summary>거부된 행동 요청 이벤트를 게시하고 false를 반환합니다.</summary>
            return RejectRequest();

        SetState(PlayerActionState.Guarding, PlayerActionStage.GuardStarted);
        if (_guardStartRecovery > 0f)
            BeginRecovery(_guardStartRecovery, PlayerAttackOutcome.None);
        return true;
    }

    /// <summary>가드 상태를 종료하고 설정된 가드 종료 회복 시간을 적용합니다.</summary>
    public void EndGuard()
    {
        if (_state != PlayerActionState.Guarding)
            return;

        SetState(PlayerActionState.Free, PlayerActionStage.GuardEnded);
        BeginRecovery(_guardEndRecovery, PlayerAttackOutcome.None);
    }

    /// <summary>현재 가드 상태를 검사해 적 공격을 막을 수 있는지 반환합니다.</summary>
    public bool TryBlockIncomingAttack()
    {
        return _state == PlayerActionState.Guarding;
    }

    /// <summary>적 시스템이 확정한 가드 성공을 알리고 설정된 회복을 적용합니다.</summary>
    public void NotifyGuardSuccess()
    {
        if (_state != PlayerActionState.Guarding)
            return;

        Publish(PlayerActionStage.GuardSucceeded);
        BeginRecovery(_guardSuccessRecovery, PlayerAttackOutcome.None);
    }

    /// <summary>자유 상태에서 기 모으기를 시작하고 충전 시간을 초기화합니다. 상태가 허용되지 않으면 false를 반환합니다.</summary>
    public bool BeginCharge()
    {
        if (!_enableChargeAttack || _state != PlayerActionState.Free)
            /// <summary>거부된 행동 요청 이벤트를 게시하고 false를 반환합니다.</summary>
            return RejectRequest();

        _chargeElapsed = 0f;
        _chargeReady = _minimumChargeTime <= 0f;
        SetState(PlayerActionState.Charging, PlayerActionStage.ChargeStarted);
        if (_chargeReady)
            Publish(PlayerActionStage.ChargeReady);
        return true;
    }

    /// <summary>충전을 끝내고 완료 여부에 따라 충전 결과를 게시합니다. 설정된 조기 취소 또는 완료 회복을 적용합니다.</summary>
    public void EndCharge()
    {
        if (_state != PlayerActionState.Charging)
            return;

        bool completed = _chargeReady;
        _chargeElapsed = 0f;
        _chargeReady = false;
        SetState(PlayerActionState.Free, completed ? PlayerActionStage.ChargeEnded : PlayerActionStage.ChargeCancelled);
        BeginRecovery(completed ? _chargeReleaseRecovery : _chargeEarlyCancelRecovery, PlayerAttackOutcome.None);
    }

    /// <summary>자유 상태에서 일반 공격을 시작하고 공격 방향과 행동 식별자를 고정합니다. 요청 수락 여부를 반환합니다.</summary>
    public bool RequestNormalAttack()
    {
        if (_state != PlayerActionState.Free)
            /// <summary>거부된 행동 요청 이벤트를 게시하고 false를 반환합니다.</summary>
            return RejectRequest();

        StartAttack(PlayerActionState.NormalAttack);
        _lockedAttackDirection = GetHorizontalViewDirection();
        Publish(PlayerActionStage.AttackStarted);
        return true;
    }

    /// <summary>현재 일반 공격의 적중 프레임에서 적 시스템에 판정을 요청합니다. actionId가 현재 행동과 다르면 무시합니다.</summary>
    public void NotifyNormalAttackImpact(int actionId)
    {
        if (_state != PlayerActionState.NormalAttack || actionId != _currentActionId || _attackImpactResolved)
            return;

        PlayerAttackResolution resolution = ResolveAttack(PlayerAttackKind.Normal, null, _normalAttackRange, _lockedAttackDirection);
        ApplyAttackResolution(resolution);
    }

    /// <summary>일반 공격 애니메이션 종료를 처리하고 미판정 공격을 빗나감으로 확정한 뒤 회복을 적용합니다.</summary>
    public void NotifyNormalAttackAnimationEnded(int actionId)
    {
        if (_state != PlayerActionState.NormalAttack || actionId != _currentActionId || _attackActionFinished)
            return;

        if (!_attackImpactResolved)
            ApplyAttackResolution(new PlayerAttackResolution(_currentActionId, false, null, false));

        FinishAttackAction(_normalAttackRecovery, _currentAttackOutcome);
    }

    /// <summary>완료된 기 모으기에서 감지 대상과 중앙 화면 영역으로 목표를 고정하고 발도 대시를 시작합니다. 요청 성공 여부를 반환합니다.</summary>
    public bool RequestIaidoAttack()
    {
        if (!_enableChargeAttack || _state != PlayerActionState.Charging || !_chargeReady)
            /// <summary>거부된 행동 요청 이벤트를 게시하고 false를 반환합니다.</summary>
            return RejectRequest();

        StartAttack(PlayerActionState.IaidoDash);
        _chargeElapsed = 0f;
        _chargeReady = false;

        bool foundTarget = TrySelectIaidoTarget(out PlayerCombatTarget combatTarget, out Vector3 perceivedPosition);
        if (foundTarget)
        {
            _preferredTargetId = combatTarget.TargetId;
            _lockedAttackDirection = Vector3.ProjectOnPlane(perceivedPosition - transform.position, Vector3.up).normalized;
        }
        else
        {
            _preferredTargetId = null;
            _lockedAttackDirection = GetHorizontalViewDirection();
        }

        Publish(PlayerActionStage.DashStarted);
        bool began = _motor != null && _motor.BeginIaidoDash(
            _preferredTargetId,
            foundTarget ? perceivedPosition : transform.position,
            foundTarget ? combatTarget.HitCollider : null,
            foundTarget ? combatTarget.CollisionRadius : 0f,
            _motor != null ? _motor.IaidoMaxDistance : 0f,
            _targetStandOff);

        if (!began)
        {
            SetState(PlayerActionState.Free, PlayerActionStage.RequestRejected);
            return false;
        }

        return true;
    }

    /// <summary>발도 베기의 적중 프레임에서 고정 대상 또는 전방 공격 판정을 적 시스템에 요청합니다. actionId가 다르면 무시합니다.</summary>
    public void NotifyIaidoSlashImpact(int actionId)
    {
        if (_state != PlayerActionState.IaidoSlash || actionId != _currentActionId || _attackImpactResolved)
            return;

        PlayerAttackResolution resolution = ResolveAttack(
            PlayerAttackKind.IaidoFinisher,
            _preferredTargetId,
            _iaidoAttackRange,
            _lockedAttackDirection);
        ApplyAttackResolution(resolution);
    }

    /// <summary>발도 베기 종료를 처리하고 적중·빗나감 결과에 맞는 회복 시간을 적용합니다.</summary>
    public void NotifyIaidoSlashAnimationEnded(int actionId)
    {
        if (_state != PlayerActionState.IaidoSlash || actionId != _currentActionId || _attackActionFinished)
            return;

        if (!_attackImpactResolved)
            ApplyAttackResolution(new PlayerAttackResolution(_currentActionId, false, null, false));

        float recovery = _currentAttackOutcome == PlayerAttackOutcome.Hit
            ? _iaidoHitRecovery
            : _iaidoMissRecovery;
        FinishAttackAction(recovery, _currentAttackOutcome);
    }

    /// <summary>일시정지 상태를 전환하고 복귀할 행동 상태를 보존합니다. paused가 false이면 보존한 상태를 복원합니다.</summary>
    public void SetPaused(bool paused)
    {
        if (_state == PlayerActionState.Dead || (paused == (_state == PlayerActionState.Paused)))
            return;

        if (paused)
        {
            _stateBeforePause = _state;
            _requestedMoveInput = Vector2.zero;
            _moveInput = Vector2.zero;
            _motor?.ClearInput();
            SetState(PlayerActionState.Paused, PlayerActionStage.StateChanged);
            return;
        }

        SetState(_stateBeforePause, PlayerActionStage.StateChanged);
    }

    /// <summary>사망을 최우선 상태로 설정하고 이동 입력과 진행 중인 대시를 중단합니다.</summary>
    public void SetDead()
    {
        if (_state == PlayerActionState.Dead)
            return;

        _state = PlayerActionState.Dead;
        _recoveryRemaining = 0f;
        _requestedMoveInput = Vector2.zero;
        _moveInput = Vector2.zero;
        _motor?.ClearInput();
        _motor?.CancelDash();
        Publish(PlayerActionStage.StateChanged);
    }

    /// <summary>새 공격 식별자를 만들고 적중 결과를 초기화한 뒤 지정한 공격 상태로 전환합니다.</summary>
    private void StartAttack(PlayerActionState attackState)
    {
        _currentActionId = ++_nextActionId;
        _attackImpactResolved = false;
        _attackActionFinished = false;
        _currentAttackOutcome = PlayerAttackOutcome.None;
        _preferredTargetId = null;
        SetState(attackState, PlayerActionStage.StateChanged);
    }

    /// <summary>공격 종류, 대상, 거리, 방향으로 적 시스템에 판정을 요청하고 현재 행동 식별자와 일치하는 결과를 반환합니다.</summary>
    private PlayerAttackResolution ResolveAttack(
        PlayerAttackKind kind,
        string targetId,
        float range,
        Vector3 direction)
    {
        if (_combatService == null)
            return new PlayerAttackResolution(_currentActionId, false, null, false);

        Vector3 origin = _viewCamera != null
            ? _viewCamera.transform.position
            : transform.position + Vector3.up * _attackOriginHeight;

        PlayerAttackRequest request = new PlayerAttackRequest(
            _currentActionId,
            kind,
            targetId,
            origin,
            direction,
            range,
            Time.time);

        PlayerAttackResolution result = _combatService.ResolvePlayerAttack(request);
        if (result.ActionId != _currentActionId)
            return new PlayerAttackResolution(_currentActionId, false, null, false);
        return result;
    }

    /// <summary>적중 또는 빗나감 결과를 저장하고 행동 이벤트를 게시합니다. 발도 적중 시 사망 확정 여부도 전달합니다.</summary>
    private void ApplyAttackResolution(PlayerAttackResolution resolution)
    {
        _attackImpactResolved = true;
        _currentAttackOutcome = resolution.HitConfirmed
            ? PlayerAttackOutcome.Hit
            : PlayerAttackOutcome.Miss;

        if (resolution.HitConfirmed)
        {
            Publish(PlayerActionStage.HitConfirmed, _currentAttackOutcome, resolution.HitTargetId);
            if (resolution.DeathConfirmed)
                Publish(PlayerActionStage.DeathConfirmed, _currentAttackOutcome, resolution.HitTargetId);
            else if (_state == PlayerActionState.IaidoSlash)
                Debug.LogError("Enemy combat service confirmed an iaido hit without confirming the required death.", this);
        }
        else
        {
            Publish(PlayerActionStage.Missed, _currentAttackOutcome, resolution.HitTargetId);
        }
    }

    /// <summary>공격 동작 종료를 게시하고 중복 없이 결과별 회복을 시작합니다.</summary>
    private void FinishAttackAction(float recovery, PlayerAttackOutcome outcome)
    {
        if (_attackActionFinished)
            return;

        _attackActionFinished = true;
        _recoveryRemaining = 0f;
        Publish(PlayerActionStage.ActionEnded, outcome, _preferredTargetId);
        BeginRecovery(recovery, outcome);
    }

    /// <summary>회복 시간을 0 이상으로 제한해 저장하고 공격 상태를 회복 또는 자유 상태로 전환합니다.</summary>
    private void BeginRecovery(float duration, PlayerAttackOutcome outcome)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
        {
            _recoveryRemaining = 0f;
            if (_state == PlayerActionState.NormalAttack
                || _state == PlayerActionState.IaidoSlash
                || _state == PlayerActionState.IaidoDash)
            {
                SetState(PlayerActionState.Free, PlayerActionStage.StateChanged, outcome);
                ResetFinishedAttack();
            }
            return;
        }

        _recoveryRemaining = duration;
        _moveInput = Vector2.zero;
        _motor?.ClearInput();
        SetState(PlayerActionState.Recovery, PlayerActionStage.RecoveryStarted, outcome);
    }

    /// <summary>회복 타이머를 끝내고 자유 상태로 복귀한 뒤 공격 임시 상태를 초기화합니다.</summary>
    private void EndRecovery()
    {
        _recoveryRemaining = 0f;
        SetState(PlayerActionState.Free, PlayerActionStage.RecoveryEnded, _currentAttackOutcome);
        ResetFinishedAttack();
        _moveInput = _requestedMoveInput;
        _motor?.SetMoveInput(_moveInput);
    }

    /// <summary>종료된 공격의 적중 처리, 결과, 대상 정보를 초기화합니다.</summary>
    private void ResetFinishedAttack()
    {
        _attackImpactResolved = false;
        _attackActionFinished = false;
        _currentAttackOutcome = PlayerAttackOutcome.None;
        _preferredTargetId = null;
    }

    /// <summary>대시 종료 이유에 따라 벽 충돌 회복을 적용하거나 베기 상태로 전환합니다.</summary>
    private void HandleDashEnded(PlayerDashEnd result)
    {
        if (_state != PlayerActionState.IaidoDash)
            return;

        if (result.Reason == PlayerDashEndReason.WallBlocked)
        {
            Publish(PlayerActionStage.DashBlocked, PlayerAttackOutcome.WallBlocked, _preferredTargetId, result.Position);
            _attackActionFinished = true;
            _recoveryRemaining = 0f;
            Publish(PlayerActionStage.ActionEnded, PlayerAttackOutcome.WallBlocked, _preferredTargetId, result.Position);
            BeginRecovery(_iaidoWallBlockedRecovery, PlayerAttackOutcome.WallBlocked);
            return;
        }

        SetState(PlayerActionState.IaidoSlash, PlayerActionStage.DashArrived, PlayerAttackOutcome.None, _preferredTargetId, result.Position);
        Publish(PlayerActionStage.SlashStarted, PlayerAttackOutcome.None, _preferredTargetId, result.Position);
    }

    /// <summary>유효한 감지 대상 중 중앙 1/3, 전방, 최대 거리 조건과 안정적인 우선순위로 목표를 선택합니다. 선택 결과와 위치를 출력합니다.</summary>
    private bool TrySelectIaidoTarget(out PlayerCombatTarget selected, out Vector3 perceivedPosition)
    {
        selected = default;
        perceivedPosition = Vector3.zero;
        if (_perceptionProvider == null || _combatService == null)
            return false;

        IReadOnlyList<PlayerPerceivedTarget> targets = _perceptionProvider.GetCurrentTargets();
        if (targets == null || targets.Count == 0)
            return false;

        Camera camera = _viewCamera != null ? _viewCamera : Camera.main;
        if (camera == null)
            return false;

        bool found = false;
        float bestCenterOffset = float.MaxValue;
        float bestDistance = float.MaxValue;
        string bestId = null;
        float now = Time.time;
        float maxDistance = _motor != null ? _motor.IaidoMaxDistance : 0f;

        foreach (PlayerPerceivedTarget perceived in targets)
        {
            if (string.IsNullOrEmpty(perceived.TargetId) || now > perceived.ValidUntil)
                continue;

            Vector3 screen = camera.WorldToViewportPoint(perceived.WorldPosition);
            if (screen.z <= 0f || screen.x < 1f / 3f || screen.x > 2f / 3f)
                continue;

            Vector3 toTarget = perceived.WorldPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > maxDistance)
                continue;

            if (!_combatService.TryGetTarget(perceived.TargetId, out PlayerCombatTarget combatTarget)
                || !combatTarget.IsAlive)
                continue;

            float centerOffset = Mathf.Abs(screen.x - 0.5f);
            bool isBetter = !found
                || centerOffset < bestCenterOffset - 0.0001f
                || (Mathf.Abs(centerOffset - bestCenterOffset) <= 0.0001f && distance < bestDistance - 0.0001f)
                || (Mathf.Abs(centerOffset - bestCenterOffset) <= 0.0001f
                    && Mathf.Abs(distance - bestDistance) <= 0.0001f
                    && string.CompareOrdinal(perceived.TargetId, bestId) < 0);

            if (!isBetter)
                continue;

            found = true;
            bestCenterOffset = centerOffset;
            bestDistance = distance;
            bestId = perceived.TargetId;
            selected = combatTarget;
            perceivedPosition = perceived.WorldPosition;
        }

        return found;
    }

    /// <summary>행동 상태, 충전 완료, 웅크리기 여부로 이동 유형을 반환합니다.</summary>
    private PlayerLocomotionMode GetLocomotionMode()
    {
        if (_state == PlayerActionState.IaidoDash)
            return PlayerLocomotionMode.IaidoDash;

        bool canWalk = _state == PlayerActionState.Free
            || _state == PlayerActionState.NormalAttack
            || _state == PlayerActionState.Guarding
            || (_state == PlayerActionState.Charging && _chargeReady);
        if (_motor != null && _motor.IsCrouching)
            return canWalk && _moveInput.sqrMagnitude > 0.0001f
                ? PlayerLocomotionMode.CrouchWalking
                : PlayerLocomotionMode.Crouching;

        if (_state == PlayerActionState.Guarding)
            return PlayerLocomotionMode.GuardWalking;
        if (_state == PlayerActionState.Charging)
            return _chargeReady ? PlayerLocomotionMode.ChargingWalking : PlayerLocomotionMode.ChargingStill;
        if ((_state == PlayerActionState.Free || _state == PlayerActionState.NormalAttack) && _moveInput.sqrMagnitude > 0.0001f)
            return PlayerLocomotionMode.Walking;
        return PlayerLocomotionMode.Stopped;
    }

    /// <summary>회복, 일시정지, 사망, 발도 동작, 초기 충전 상태의 이동 제한 여부를 반환합니다.</summary>
    private bool IsMovementLocked()
    {
        return _state == PlayerActionState.Recovery
            || _state == PlayerActionState.Paused
            || _state == PlayerActionState.Dead
            || _state == PlayerActionState.IaidoDash
            || _state == PlayerActionState.IaidoSlash
            || (_state == PlayerActionState.Charging && !_chargeReady);
    }

    /// <summary>카메라 또는 플레이어 정면에서 수평 시선 방향을 계산해 반환합니다.</summary>
    private Vector3 GetHorizontalViewDirection()
    {
        Transform source = _viewCamera != null ? _viewCamera.transform : transform;
        Vector3 direction = Vector3.ProjectOnPlane(source.forward, Vector3.up).normalized;
        return direction.sqrMagnitude > 0.0001f ? direction : transform.forward;
    }

    /// <summary>이동·집중·충전 상태가 바뀌면 감지 시스템과 구독자에게 새 감각 문맥을 전달합니다.</summary>
    private void PublishSenseContext(PlayerLocomotionMode mode)
    {
        bool moving = _motor != null && _motor.HorizontalSpeed > 0.05f;
        bool canFocus = _state == PlayerActionState.Free || _state == PlayerActionState.Charging;
        PlayerSenseContext context = new PlayerSenseContext(
            moving,
            canFocus && !moving,
            _state == PlayerActionState.Charging,
            mode);

        if (_hasSenseContext
            && _lastSenseContext.IsMoving == context.IsMoving
            && _lastSenseContext.IsFocusing == context.IsFocusing
            && _lastSenseContext.IsCharging == context.IsCharging
            && _lastSenseContext.LocomotionMode == context.LocomotionMode)
            return;

        _hasSenseContext = true;
        _lastSenseContext = context;
        _perceptionProvider?.SetPlayerSenseContext(context);
        SenseContextChanged?.Invoke(context);
    }

    /// <summary>Inspector에 연결된 MonoBehaviour를 플레이어 서비스 계약으로 변환해 등록합니다.</summary>
    private void BindConfiguredServices()
    {
        ConfigureServices(
            _perceptionProviderSource as IPlayerPerceptionProvider,
            _combatServiceSource as IPlayerCombatService,
            _mapServiceSource as IPlayerMapService);
    }

    /// <summary>PlayerMotor에서 발생한 발걸음 이벤트를 플레이어 시스템 구독자에게 전달합니다.</summary>
    private void ForwardFootstep(PlayerFootstepEvent step)
    {
        FootstepEmitted?.Invoke(step);
    }

    /// <summary>행동 상태를 갱신하고 결과·대상·위치를 포함한 상태 이벤트를 게시합니다.</summary>
    private void SetState(
        PlayerActionState next,
        PlayerActionStage stage,
        PlayerAttackOutcome outcome = PlayerAttackOutcome.None,
        string targetId = null,
        Vector3 worldPosition = default)
    {
        _state = next;
        Publish(stage, outcome, targetId, worldPosition);
        _hasSenseContext = false;
    }

    /// <summary>거부된 행동 요청 이벤트를 게시하고 false를 반환합니다.</summary>
    private bool RejectRequest()
    {
        Publish(PlayerActionStage.RequestRejected);
        return false;
    }

    /// <summary>현재 행동 식별자와 지정한 단계·결과 정보를 PlayerActionEvent로 게시합니다.</summary>
    private void Publish(
        PlayerActionStage stage,
        PlayerAttackOutcome outcome = PlayerAttackOutcome.None,
        string targetId = null,
        Vector3 worldPosition = default)
    {
        ActionEvent?.Invoke(new PlayerActionEvent(
            _currentActionId,
            _state,
            stage,
            outcome,
            targetId,
            worldPosition));
    }
}

