using System;
using UnityEngine;

public enum PlayerDashEndReason
{
    TargetReached,
    MaxDistanceReached,
    WallBlocked
}

public struct PlayerDashEnd
{
    public PlayerDashEndReason Reason { get; }
    public string TargetId { get; }
    public Vector3 Position { get; }

    /// <summary>대시 종료 이유, 대상 식별자, 종료 위치로 불변 종료 결과를 생성합니다.</summary>
    public PlayerDashEnd(PlayerDashEndReason reason, string targetId, Vector3 position)
    {
        Reason = reason;
        TargetId = targetId;
        Position = position;
    }
}

/// <summary>
/// Executes first-person locomotion and physical iaido travel. It does not read input or create sensory effects.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Transform _cameraPivot;

    [Header("Locomotion")]
    [SerializeField, Min(0f)] private float _walkSpeed = 2f;
    [SerializeField, Min(0f)] private float _guardWalkSpeed = 1f;
    [SerializeField, Min(0f)] private float _chargingWalkSpeed = 0.8f;
    [SerializeField, Min(0f)] private float _acceleration = 8f;
    [SerializeField, Min(0f)] private float _deceleration = 12f;
    [SerializeField, Min(0f)] private float _gravity = 20f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float _lookSensitivity = 0.12f;
    [SerializeField, Range(1f, 89f)] private float _pitchLimit = 75f;

    [Header("Footsteps")]
    [SerializeField, Min(0.1f)] private float _normalStepDistance = 1.2f;
    [SerializeField, Min(0.1f)] private float _quietStepDistance = 1.5f;
    [SerializeField, Range(0f, 1f)] private float _normalFootstepNoise = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _quietFootstepNoise = 0.2f;

    [Header("Iaido travel")]
    [SerializeField, Min(0f)] private float _iaidoSpeed = 14f;
    [SerializeField, Min(0f)] private float _iaidoMaxDistance = 7f;
    [SerializeField] private LayerMask _dashCollisionMask = ~0;

    private Vector2 _moveInput;
    private Vector2 _pendingLookDelta;
    private Vector3 _planarVelocity;
    private float _verticalVelocity;
    private float _yaw;
    private float _pitch;
    private float _stepDistanceAccumulated;
    private bool _leftStep;
    private bool _isDashing;
    private Vector3 _dashDirection;
    private float _dashGoalDistance;
    private float _dashDistanceTravelled;
    private string _dashTargetId;
    private Collider _dashTargetCollider;
    private IPlayerMapService _mapService;
    private IPlayerCombatService _combatService;
    private float _lastHorizontalSpeed;

    public event Action<PlayerFootstepEvent> FootstepEmitted;
    public event Action<PlayerDashEnd> DashEnded;

    public float HorizontalSpeed => _lastHorizontalSpeed;
    public float IaidoMaxDistance => _iaidoMaxDistance;
    
    public float WalkSpeed => _walkSpeed;
    public float Acceleration => _acceleration;
    public float Deceleration => _deceleration;
public bool IsDashing => _isDashing;

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_cameraPivot == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
                _cameraPivot = childCamera.transform;
        }

        _yaw = transform.eulerAngles.y;
        if (_cameraPivot != null)
            _pitch = NormalizeAngle(_cameraPivot.localEulerAngles.x);
    }

    /// <summary>벽 충돌을 소유하는 맵 서비스 계약을 이동 장치에 등록합니다.</summary>
    public void ConfigureMapService(IPlayerMapService service)
    {
        _mapService = service;
    }

    /// <summary>적 충돌을 식별하는 전투 서비스 계약을 이동 장치에 등록합니다.</summary>
    public void ConfigureCombatService(IPlayerCombatService service)
    {
        _combatService = service;
    }

    /// <summary>정규화된 이동 방향 입력을 저장합니다. 이 값은 다음 Tick의 목표 속도 계산에 사용됩니다.</summary>
    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>시점 변화량을 누적해 다음 Tick에서 카메라 회전에 반영합니다.</summary>
    public void AddLookDelta(Vector2 delta)
    {
        _pendingLookDelta += delta;
    }

    /// <summary>이동과 시점 입력, 현재 평면 속도를 초기화합니다.</summary>
    public void ClearInput()
    {
        _moveInput = Vector2.zero;
        _pendingLookDelta = Vector2.zero;
        _planarVelocity = Vector3.zero;
    }

    /// <summary>이동 유형, 잠금 상태, 게임 시간 간격으로 시점·이동·중력·발자국을 갱신합니다.</summary>
    public void Tick(PlayerLocomotionMode locomotionMode, bool movementLocked, bool lookLocked, float deltaTime)
    {
        if (_characterController == null || !_characterController.enabled)
            return;

        if (!lookLocked)
            ApplyLook();
        else
            _pendingLookDelta = Vector2.zero;

        if (_isDashing)
        {
            TickDash(deltaTime);
            _lastHorizontalSpeed = 0f;
            return;
        }

        Vector3 desiredVelocity = Vector3.zero;
        if (!movementLocked)
        {
            float speed = GetSpeed(locomotionMode);
            if (speed > 0f && _moveInput.sqrMagnitude > 0f)
            {
                Vector3 local = new Vector3(_moveInput.x, 0f, _moveInput.y);
                desiredVelocity = transform.TransformDirection(local.normalized) * speed * Mathf.Min(1f, _moveInput.magnitude);
            }
        }

        float rate = desiredVelocity.sqrMagnitude > 0.001f ? _acceleration : _deceleration;
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredVelocity, rate * deltaTime);

        bool wasGrounded = _characterController.isGrounded;
        if (wasGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity -= _gravity * deltaTime;

        Vector3 before = transform.position;
        _characterController.Move((_planarVelocity + Vector3.up * _verticalVelocity) * deltaTime);
        Vector3 actualDelta = transform.position - before;
        actualDelta.y = 0f;
        float actualDistance = actualDelta.magnitude;
        _lastHorizontalSpeed = deltaTime > 0f ? actualDistance / deltaTime : 0f;

        bool walkingMode = locomotionMode == PlayerLocomotionMode.Walking
            || locomotionMode == PlayerLocomotionMode.GuardWalking
            || locomotionMode == PlayerLocomotionMode.ChargingWalking;
        bool groundedAfterMove = _characterController.isGrounded;
        if (walkingMode && !movementLocked && wasGrounded && groundedAfterMove && actualDistance > 0.0001f)
            AccumulateFootsteps(actualDistance, locomotionMode);
        else if (_moveInput.sqrMagnitude <= 0.0001f || movementLocked || !groundedAfterMove)
            _stepDistanceAccumulated = 0f;
    }

    /// <summary>대상 위치 또는 전방 방향으로 목표 거리를 계산하고 대시를 시작합니다. 시작 가능 여부를 반환합니다.</summary>
    public bool BeginIaidoDash(
        string targetId,
        Vector3 targetPosition,
        Collider targetCollider,
        float targetCollisionRadius,
        float maxDistance,
        float standOffDistance)
    {
        if (_isDashing || _characterController == null || !_characterController.enabled)
            return false;

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        bool hasTarget = !string.IsNullOrEmpty(targetId);
        Vector3 direction = hasTarget ? toTarget.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        _dashDirection = direction.normalized;
        _dashTargetId = hasTarget ? targetId : null;
        _dashTargetCollider = targetCollider;
        _dashDistanceTravelled = 0f;

        float requestedDistance = Mathf.Max(0f, maxDistance);
        if (hasTarget)
        {
            float targetExtent = GetTargetExtent(targetCollider, targetCollisionRadius, _dashDirection);
            float centerDistance = toTarget.magnitude;
            float playerRadius = _characterController.radius + _characterController.skinWidth;
            requestedDistance = Mathf.Clamp(
                centerDistance - targetExtent - playerRadius - Mathf.Max(0f, standOffDistance),
                0f,
                requestedDistance);
        }

        _dashGoalDistance = requestedDistance;
        _planarVelocity = Vector3.zero;
        _moveInput = Vector2.zero;
        _isDashing = true;

        if (_dashGoalDistance <= 0.001f)
            FinishDash(hasTarget ? PlayerDashEndReason.TargetReached : PlayerDashEndReason.MaxDistanceReached);

        return true;
    }

    /// <summary>진행 중인 대시를 벽 충돌 종료로 처리하고 종료 이벤트를 보냅니다.</summary>
    public void CancelDash()
    {
        if (_isDashing)
            FinishDash(PlayerDashEndReason.WallBlocked);
    }

    /// <summary>대시 거리, 벽·적 충돌, 중력과 도착 여부를 갱신합니다.</summary>
private void TickDash(float deltaTime)
    {
        float remaining = _dashGoalDistance - _dashDistanceTravelled;
        if (remaining <= 0.001f)
        {
            FinishDash(string.IsNullOrEmpty(_dashTargetId)
                ? PlayerDashEndReason.MaxDistanceReached
                : PlayerDashEndReason.TargetReached);
            return;
        }

        float step = Mathf.Min(_iaidoSpeed * deltaTime, remaining);
        if (step <= 0f)
            return;

        if (TryGetFirstDashObstacle(_dashDirection, step, out RaycastHit hit))
        {
            bool hitSelectedTarget =
                _combatService != null
                && !string.IsNullOrEmpty(_dashTargetId)
                && _combatService.TryGetTarget(hit.collider, out PlayerCombatTarget contactedTarget)
                && contactedTarget.IsAlive
                && contactedTarget.TargetId == _dashTargetId;

            if (hitSelectedTarget)
            {
                FinishDash(PlayerDashEndReason.TargetReached);
                return;
            }

            PlayerWallResolution resolution = _mapService != null
                ? _mapService.ResolveDashWallContact(hit.collider, hit.point, hit.normal)
                : new PlayerWallResolution(false, false, false);

            if (!resolution.AllowsDashContinuation)
            {
                FinishDash(PlayerDashEndReason.WallBlocked);
                return;
            }

            // The map owner must update the physical obstacle before reporting it passable.
            if (hit.collider != null && hit.collider.enabled && hit.collider.gameObject.activeInHierarchy)
            {
                FinishDash(PlayerDashEndReason.WallBlocked);
                return;
            }
        }

        bool grounded = _characterController.isGrounded;
        if (grounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity -= _gravity * deltaTime;

        Vector3 before = transform.position;
        _characterController.Move((_dashDirection + Vector3.up * (_verticalVelocity / Mathf.Max(_iaidoSpeed, 0.01f))) * step);
        Vector3 moved = transform.position - before;
        moved.y = 0f;
        float actualDistance = Vector3.Dot(moved, _dashDirection);
        if (actualDistance <= 0.001f)
        {
            FinishDash(PlayerDashEndReason.WallBlocked);
            return;
        }

        _dashDistanceTravelled += actualDistance;
        if (_dashDistanceTravelled >= _dashGoalDistance - 0.001f)
        {
            FinishDash(string.IsNullOrEmpty(_dashTargetId)
                ? PlayerDashEndReason.MaxDistanceReached
                : PlayerDashEndReason.TargetReached);
        }
    }

    /// <summary>지정 방향과 거리에서 플레이어 자신의 충돌체를 제외한 가장 가까운 장애물이나 목표 적을 반환합니다.</summary>
private bool TryGetFirstDashObstacle(Vector3 direction, float distance, out RaycastHit closestHit)
    {
        closestHit = default;
        Vector3 worldCenter = transform.TransformPoint(_characterController.center);
        float radius = Mathf.Max(0.01f, _characterController.radius - _characterController.skinWidth);
        float halfSegment = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
        Vector3 top = worldCenter + Vector3.up * halfSegment;
        Vector3 bottom = worldCenter - Vector3.up * halfSegment;

        RaycastHit[] hits = Physics.CapsuleCastAll(
            top,
            bottom,
            radius,
            direction,
            distance + _characterController.skinWidth,
            _dashCollisionMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearest = float.MaxValue;
        foreach (RaycastHit candidate in hits)
        {
            Collider hitCollider = candidate.collider;
            if (hitCollider == null || IsOwnCollider(hitCollider))
                continue;

            if (candidate.distance < nearest)
            {
                nearest = candidate.distance;
                closestHit = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>후보 충돌체가 플레이어 본체 또는 자식인지 반환합니다.</summary>
    private bool IsOwnCollider(Collider candidate)
    {
        return candidate.transform == transform || candidate.transform.IsChildOf(transform);
    }

    /// <summary>후보 충돌체가 현재 선택한 대상 충돌체인지 반환합니다.</summary>


    /// <summary>방향별 대상 충돌체 반경을 계산하고 충돌체가 없으면 지정 반경을 반환합니다.</summary>
    private static float GetTargetExtent(Collider targetCollider, float fallbackRadius, Vector3 direction)
    {
        if (targetCollider == null)
            return Mathf.Max(0f, fallbackRadius);

        Bounds bounds = targetCollider.bounds;
        Vector3 extents = bounds.extents;
        return Mathf.Abs(direction.x) * extents.x
             + Mathf.Abs(direction.y) * extents.y
             + Mathf.Abs(direction.z) * extents.z;
    }

    /// <summary>대시를 종료하고 이유, 대상, 위치를 DashEnded 구독자에게 전달합니다.</summary>
    private void FinishDash(PlayerDashEndReason reason)
    {
        if (!_isDashing)
            return;

        _isDashing = false;
        _planarVelocity = Vector3.zero;
        _moveInput = Vector2.zero;
        PlayerDashEnd result = new PlayerDashEnd(reason, _dashTargetId, transform.position);
        _dashTargetId = null;
        _dashTargetCollider = null;
        DashEnded?.Invoke(result);
    }

    /// <summary>누적 시점 변화량으로 플레이어 yaw와 카메라 pitch를 갱신합니다.</summary>
    private void ApplyLook()
    {
        if (_pendingLookDelta.sqrMagnitude <= 0f)
            return;

        _yaw += _pendingLookDelta.x * _lookSensitivity;
        _pitch = Mathf.Clamp(_pitch - _pendingLookDelta.y * _lookSensitivity, -_pitchLimit, _pitchLimit);
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        _pendingLookDelta = Vector2.zero;
    }

    /// <summary>이동 유형에 맞는 Inspector 이동 속도를 반환합니다.</summary>
    private float GetSpeed(PlayerLocomotionMode mode)
    {
        switch (mode)
        {
            case PlayerLocomotionMode.Walking:
                return _walkSpeed;
            case PlayerLocomotionMode.GuardWalking:
                return _guardWalkSpeed;
            case PlayerLocomotionMode.ChargingWalking:
                return _chargingWalkSpeed;
            default:
                return 0f;
        }
    }

    /// <summary>실제 이동 거리로 발걸음을 계산하고 보행 유형, 소음, 위치 정보를 이벤트로 발행합니다.</summary>
    private void AccumulateFootsteps(float distance, PlayerLocomotionMode mode)
    {
        bool quiet = mode == PlayerLocomotionMode.ChargingWalking;
        float stride = quiet ? _quietStepDistance : _normalStepDistance;
        float noise = quiet ? _quietFootstepNoise : _normalFootstepNoise;
        _stepDistanceAccumulated += distance;

        while (_stepDistanceAccumulated >= stride)
        {
            _stepDistanceAccumulated -= stride;
            _leftStep = !_leftStep;
            Vector3 position = transform.position - transform.forward * 0.05f
                + transform.right * (_leftStep ? -0.12f : 0.12f);

            FootstepEmitted?.Invoke(new PlayerFootstepEvent(
                position,
                Time.time,
                quiet ? PlayerFootstepType.Quiet : PlayerFootstepType.Normal,
                _lastHorizontalSpeed,
                noise));
        }
    }

    /// <summary>Unity 각도 값을 -180도에서 180도 범위로 정규화합니다.</summary>
    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }

    private void OnDisable()
    {
        _isDashing = false;
        _moveInput = Vector2.zero;
        _pendingLookDelta = Vector2.zero;
        _planarVelocity = Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _iaidoMaxDistance = Mathf.Max(0f, _iaidoMaxDistance);
        _iaidoSpeed = Mathf.Max(0f, _iaidoSpeed);
    }
#endif
}

