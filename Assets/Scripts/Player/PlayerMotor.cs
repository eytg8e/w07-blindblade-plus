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
    [SerializeField, Min(0f)] private float _walkSpeed = 1.388889f;
    [SerializeField, Min(0f)] private float _guardWalkSpeed = 0.6944445f;
    [SerializeField, Min(0f)] private float _chargingWalkSpeed = 0.5555556f;
    [SerializeField, Min(0.01f)] private float _acceleration = 8f;
    [SerializeField, Min(0f)] private float _deceleration = 12f;
    [SerializeField, Min(0f)] private float _gravity = 20f;

    [Header("Crouch")]
    [SerializeField, Min(0f)] private float _crouchWalkSpeed = 0.7f;
    [SerializeField, Min(0.1f)] private float _crouchedCharacterHeight = 1.6f;
    [SerializeField, Min(0f)] private float _crouchedCameraHeight = 1.325f;
    [SerializeField, Min(0.01f)] private float _crouchTransitionDuration = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _crouchFootstepNoise = 0.12f;
    [SerializeField] private LayerMask _crouchCollisionMask = ~0;

    [Header("Look")]
    [SerializeField, Min(0f)] private float _lookSensitivity = 0.12f;
    [SerializeField, Range(1f, 89f)] private float _pitchLimit = 75f;

    [Header("Footsteps")]
    [SerializeField, Min(0.01f), Tooltip("보행 중 발소리 사이의 시간. 이동을 멈추는 구간은 만들지 않습니다.")] private float _stepInterval = 0.36f;
    [SerializeField, Range(0f, 1f)] private float _normalFootstepNoise = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _quietFootstepNoise = 0.2f;

    [Header("Iaido travel")]
    [SerializeField, Min(0f)] private float _iaidoSpeed = 14f;
    [SerializeField, Min(0f)] private float _iaidoMaxDistance = 7f;
    [SerializeField] private LayerMask _dashCollisionMask = ~0;

    [Header("짧은 회피")]
    [SerializeField, Min(0.1f)] private float _dodgeDistance = 1.2f;
    [SerializeField, Min(0.05f)] private float _dodgeDuration = 0.18f;
    [SerializeField, Min(0f)] private float _dodgeCooldown = 0.6f;
    private bool _isDodging;
    private Vector3 _dodgeDirection;
    private float _dodgeElapsed;
    private float _dodgeProgress;
    private float _nextDodgeTime;

    private Vector2 _moveInput;
    private Vector2 _pendingLookDelta;
    private Vector3 _planarVelocity;
    private float _verticalVelocity;
    private float _yaw;
    private float _pitch;
    private float _stepDistanceAccumulated;
    private float _stepElapsed;
    private bool _stepInProgress;
    private PlayerLocomotionMode _stepMode;
    private bool _leftStep;
    private readonly Collider[] _crouchOverlapBuffer = new Collider[16];
    private Vector3 _standingCharacterCenter;
    private Vector3 _standingCameraLocalPosition;
    private float _standingCharacterHeight;
    private float _crouchBlend;
    private bool _crouchRequested;
    private bool _isDashing;
    private Vector3 _dashDirection;
    private float _dashGoalDistance;
    private float _dashDistanceTravelled;
    private string _dashTargetId;
    private Collider _dashTargetCollider;
    private IPlayerMapService _mapService;
    private IPlayerCombatService _combatService;
    private float _lastHorizontalSpeed;

    [Header("전투 카메라 반동")]
    [SerializeField, Min(0f)] private float _recoilReturnSpeed = 18f;
    private Vector3 _recoilEuler;
    private Vector3 _recoilOffset;
    public Vector3 ViewForward => Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(_pitch, 0f, 0f) * Vector3.forward;
    public Vector3 ViewOrigin => _cameraPivot.parent.TransformPoint(GetCameraBasePosition());

    /// <summary>direction과 strength로 시각 반동을 누적한다. 논리적인 시점·조준 방향과 캐릭터 위치는 유지한다.</summary>
    public void AddCombatRecoil(Vector3 direction, float strength)
    {
        float side = Vector3.Dot(transform.right, direction) < 0f ? -1f : 1f;
        _recoilEuler = Vector3.ClampMagnitude(_recoilEuler + new Vector3(strength, 0f, side * strength * 1.5f), 5f);
        _recoilOffset = Vector3.ClampMagnitude(_recoilOffset + Vector3.back * (0.025f * strength), 0.08f);
    }

    /// <summary>웅크리기 진행도와 원래 카메라 위치로 반동 없는 로컬 시점 위치를 반환한다.</summary>
    private Vector3 GetCameraBasePosition()
    {
        Vector3 position = _standingCameraLocalPosition;
        position.y = Mathf.Lerp(_standingCameraLocalPosition.y, _crouchedCameraHeight, Mathf.SmoothStep(0f, 1f, _crouchBlend));
        return position;
    }

    void LateUpdate()
    {
        if (_cameraPivot == null) return;
        float decay = Mathf.Exp(-_recoilReturnSpeed * Time.deltaTime);
        _recoilEuler *= decay;
        _recoilOffset *= decay;
        _cameraPivot.localPosition = GetCameraBasePosition() + _recoilOffset;
        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * Quaternion.Euler(_recoilEuler);
    }

    public event Action<PlayerFootstepEvent> FootstepEmitted;
    public event Action<PlayerDashEnd> DashEnded;

    public float HorizontalSpeed => _lastHorizontalSpeed;
    public float IaidoMaxDistance => _iaidoMaxDistance;
    
    public float WalkSpeed => _walkSpeed;
    public float Acceleration => _acceleration;
    public float Deceleration => _deceleration;
    public bool IsCrouching => _crouchBlend > 0.001f;
    public bool IsDashing => _isDashing;
    public bool IsDodging => _isDodging;
    public float DodgeCooldownRemaining => Mathf.Max(0f, _nextDodgeTime - Time.time);

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
        _standingCharacterHeight = _characterController.height;
        _standingCharacterCenter = _characterController.center;
        _crouchedCharacterHeight = Mathf.Clamp(
            _crouchedCharacterHeight, _characterController.radius * 2f, _standingCharacterHeight);
        if (_cameraPivot != null)
        {
            _pitch = NormalizeAngle(_cameraPivot.localEulerAngles.x);
            _standingCameraLocalPosition = _cameraPivot.localPosition;
            _crouchedCameraHeight = Mathf.Clamp(
                _crouchedCameraHeight, 0f, Mathf.Max(0f, _standingCameraLocalPosition.y));
        }
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

    /// <summary>웅크리기 목표 상태를 저장해 다음 Tick에서 카메라와 충돌 캡슐에 반영합니다.</summary>
    public void SetCrouchInput(bool isCrouching)
    {
        _crouchRequested = isCrouching;
    }

    /// <summary>시점 변화량을 누적해 다음 Tick에서 카메라 회전에 반영합니다.</summary>
    public void AddLookDelta(Vector2 delta)
    {
        _pendingLookDelta += delta;
    }

    /// <summary>이동과 시점 입력, 현재 평면 속도를 초기화합니다.</summary>
    public void ClearInput()
    {
        CancelDodge();
        _moveInput = Vector2.zero;
        _pendingLookDelta = Vector2.zero;
        _planarVelocity = Vector3.zero;
        ResetWalkStep();
    }

    /// <summary>이동 유형, 잠금 상태, 게임 시간 간격으로 시점·이동·중력·발자국을 갱신합니다.</summary>
    public void Tick(PlayerLocomotionMode locomotionMode, bool movementLocked, bool lookLocked, float deltaTime)
    {
        if (_characterController == null || !_characterController.enabled)
            return;

        TickCrouch(deltaTime);

        if (!lookLocked)
            ApplyLook();
        else
            _pendingLookDelta = Vector2.zero;

        if (movementLocked && _isDodging) CancelDodge();

        if (_isDashing)
        {
            TickDash(deltaTime);
            _lastHorizontalSpeed = 0f;
            return;
        }

        bool wasGrounded = _characterController.isGrounded;
        if (wasGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity -= _gravity * deltaTime;

        if (_isDodging)
        {
            TickDodge(deltaTime);
            return;
        }

        Vector3 before = transform.position;
        if (movementLocked)
        {
            _planarVelocity = Vector3.zero;
            ResetWalkStep();
            MoveWalkSegment(Vector3.zero, deltaTime);
        }
        else if (GetSpeed(locomotionMode) > 0f && _moveInput.sqrMagnitude > 0.0001f)
        {
            TickSteppedWalk(locomotionMode, deltaTime);
        }
        else
        {
            TickWalkStop(deltaTime);
        }
        Vector3 actualDelta = transform.position - before;
        actualDelta.y = 0f;
        float actualDistance = actualDelta.magnitude;
        _lastHorizontalSpeed = deltaTime > 0f ? actualDistance / deltaTime : 0f;

    }

    /// <summary>웅크리기 진행도를 부드럽게 갱신합니다. 일어설 때 머리 공간이 막혀 있으면 현재 높이를 유지합니다.</summary>
    private void TickCrouch(float deltaTime)
    {
        float targetBlend = _crouchRequested ? 1f : 0f;
        float blendStep = Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, _crouchTransitionDuration);
        float nextBlend = Mathf.MoveTowards(_crouchBlend, targetBlend, blendStep);
        if (nextBlend < _crouchBlend && !CanFitCapsule(nextBlend))
            nextBlend = _crouchBlend;

        _crouchBlend = nextBlend;
        float easedBlend = Mathf.SmoothStep(0f, 1f, _crouchBlend);
        float height = Mathf.Lerp(_standingCharacterHeight, _crouchedCharacterHeight, easedBlend);
        Vector3 center = _standingCharacterCenter;
        center.y += (_standingCharacterHeight - height) * 0.5f;
        _characterController.height = height;
        _characterController.center = center;

        if (_cameraPivot != null)
        {
            Vector3 cameraPosition = _standingCameraLocalPosition;
            cameraPosition.y = Mathf.Lerp(_standingCameraLocalPosition.y, _crouchedCameraHeight, easedBlend);
            _cameraPivot.localPosition = cameraPosition;
        }
    }

    /// <summary>blend에 해당하는 캡슐이 다른 물리 충돌체와 겹치지 않는지 반환합니다.</summary>
    private bool CanFitCapsule(float blend)
    {
        float easedBlend = Mathf.SmoothStep(0f, 1f, blend);
        float height = Mathf.Lerp(_standingCharacterHeight, _crouchedCharacterHeight, easedBlend);
        float radius = Mathf.Max(0.01f, _characterController.radius - _characterController.skinWidth - 0.01f);
        Vector3 center = _standingCharacterCenter;
        center.y += (_standingCharacterHeight - height) * 0.5f;
        Vector3 worldCenter = transform.TransformPoint(center);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 top = worldCenter + transform.up * halfSegment;
        Vector3 bottom = worldCenter - transform.up * halfSegment;
        int count = Physics.OverlapCapsuleNonAlloc(
            bottom, top, radius, _crouchOverlapBuffer, _crouchCollisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = _crouchOverlapBuffer[i];
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform))
                continue;
            if (other.bounds.max.y <= worldCenter.y - height * 0.5f + 0.02f)
                continue;
            return false;
        }

        return count < _crouchOverlapBuffer.Length;
    }

    /// <summary>input의 전후·좌우 방향을 시작 시점의 수평 방향으로 고정하고 짧은 회피를 시작한다. 접지·재사용 조건을 충족하면 true를 반환한다.</summary>
    public bool TryBeginDodge(Vector2 input)
    {
        if (_isDodging || _isDashing || !_characterController.enabled || !_characterController.isGrounded
            || Time.time < _nextDodgeTime) return false;
        Vector2 direction = input;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
        direction.Normalize();
        _dodgeDirection = (transform.right * direction.x + transform.forward * direction.y).normalized;
        _dodgeElapsed = 0f;
        _dodgeProgress = 0f;
        _nextDodgeTime = Time.time + _dodgeCooldown;
        _isDodging = true;
        _planarVelocity = Vector3.zero;
        ResetWalkStep();
        AddCombatRecoil(_dodgeDirection, 0.15f);
        return true;
    }

    /// <summary>진행 중인 회피와 평면 속도를 정리한다. 이미 사용한 재사용 시간은 유지한다.</summary>
    public void CancelDodge()
    {
        _isDodging = false;
        _planarVelocity = Vector3.zero;
    }

    /// <summary>deltaTime으로 회피 거리를 감속 곡선에 따라 이동한다. CharacterController 충돌·중력을 유지하고 벽·낙하·시간 종료 시 회피를 마친다.</summary>
    private void TickDodge(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        _dodgeElapsed = Mathf.Min(_dodgeDuration, _dodgeElapsed + deltaTime);
        float normalized = _dodgeElapsed / _dodgeDuration;
        float progress = 1f - (1f - normalized) * (1f - normalized);
        Vector3 before = transform.position;
        CollisionFlags flags = _characterController.Move(_dodgeDirection * (_dodgeDistance * (progress - _dodgeProgress))
            + Vector3.up * (_verticalVelocity * deltaTime));
        _lastHorizontalSpeed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / deltaTime;
        _dodgeProgress = progress;
        if ((flags & CollisionFlags.Sides) != 0 || !_characterController.isGrounded || normalized >= 1f) CancelDodge();
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
        ResetWalkStep();
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
            case PlayerLocomotionMode.CrouchWalking:
                return _crouchWalkSpeed;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// mode와 이동 입력으로 목표 속도에 부드럽게 접근하고 deltaTime 동안 연속 이동한다.
    /// 발소리 주기는 이동 속도와 독립적으로 진행하며 걸음 경계에서도 속도를 유지한다.
    /// </summary>
    private void TickSteppedWalk(PlayerLocomotionMode mode, float deltaTime)
    {
        Vector3 direction = transform.TransformDirection(new Vector3(_moveInput.x, 0f, _moveInput.y));
        Vector3 targetVelocity = direction * GetSpeed(mode);
        float interval = Mathf.Max(0.01f, _stepInterval);
        float remaining = deltaTime;
        while (remaining > 0.000001f)
        {
            _stepInProgress = true;
            _stepMode = mode;
            float slice = Mathf.Min(remaining, Mathf.Max(0.000001f, interval - _stepElapsed));
            MoveTowardsWalkVelocity(targetVelocity, _acceleration, slice);
            _stepElapsed += slice;
            remaining = Mathf.Max(0f, remaining - slice);
            if (_stepElapsed + 0.000001f >= interval)
                CompleteWalkStep();
        }
    }

    /// <summary>
    /// deltaTime과 감속 수치로 입력 해제 후 남은 속도를 줄여 짧게 정지한다.
    /// 실제 움직인 걸음은 완전히 정지했을 때 한 번 착지시키며 남은 이동은 충돌을 적용한다.
    /// </summary>
    private void TickWalkStop(float deltaTime)
    {
        MoveTowardsWalkVelocity(Vector3.zero, _deceleration, deltaTime);
        if (_stepInProgress)
            _stepElapsed += deltaTime;
        if (_stepInProgress && _planarVelocity.sqrMagnitude <= 0.0001f)
            CompleteWalkStep();
    }

    /// <summary>targetVelocity에 acceleration으로 접근하고 deltaTime 안의 가속 구간과 등속 구간을 적분해 이동한다.</summary>
    private void MoveTowardsWalkVelocity(Vector3 targetVelocity, float acceleration, float deltaTime)
    {
        Vector3 previousVelocity = _planarVelocity;
        float changeDuration = Mathf.Min(deltaTime,
            (targetVelocity - previousVelocity).magnitude / Mathf.Max(0.01f, acceleration));
        _planarVelocity = Vector3.MoveTowards(previousVelocity, targetVelocity,
            Mathf.Max(0.01f, acceleration) * deltaTime);
        Vector3 displacement = (previousVelocity + _planarVelocity) * (0.5f * changeDuration)
            + _planarVelocity * (deltaTime - changeDuration);
        MoveWalkSegment(displacement, deltaTime);
    }

    /// <summary>
    /// horizontalDelta와 duration으로 충돌 및 중력을 반영한 이동을 수행한다.
    /// 접지 중 실제 수평 이동 거리만 현재 걸음에 누적하며 벽에 막힌 이동은 발소리에 포함하지 않는다.
    /// </summary>
    private void MoveWalkSegment(Vector3 horizontalDelta, float duration)
    {
        Vector3 before = transform.position;
        bool grounded = _characterController.isGrounded;
        _characterController.Move(horizontalDelta + Vector3.up * (_verticalVelocity * duration));
        Vector3 actual = Vector3.ProjectOnPlane(transform.position - before, Vector3.up);
        _lastHorizontalSpeed = duration > 0f ? actual.magnitude / duration : 0f;
        if (_stepInProgress && grounded && _characterController.isGrounded)
            _stepDistanceAccumulated += actual.magnitude;
    }

    /// <summary>
    /// 완료된 걸음의 실제 이동량과 보행 유형으로 착지 이벤트를 한 번 발행한다.
    /// 이동하지 않았거나 지면을 잃은 경우 소리를 생략하고 이동 속도는 유지한다.
    /// </summary>
    private void CompleteWalkStep()
    {
        bool emit = _stepDistanceAccumulated > 0.02f && _characterController.isGrounded;
        float speed = _stepDistanceAccumulated / Mathf.Max(0.01f, _stepElapsed);
        PlayerLocomotionMode mode = _stepMode;
        _stepInProgress = false;
        _stepElapsed = 0f;
        _stepDistanceAccumulated = 0f;
        if (!emit)
            return;

        bool crouching = mode == PlayerLocomotionMode.CrouchWalking;
        bool quiet = crouching || mode == PlayerLocomotionMode.ChargingWalking;
        float noiseStrength = crouching ? _crouchFootstepNoise
            : quiet ? _quietFootstepNoise : _normalFootstepNoise;
        _leftStep = !_leftStep;
        Vector3 position = transform.position - transform.forward * 0.05f
            + transform.right * (_leftStep ? -0.12f : 0.12f);
        FootstepEmitted?.Invoke(new PlayerFootstepEvent(
            position, Time.time, quiet ? PlayerFootstepType.Quiet : PlayerFootstepType.Normal,
            speed, noiseStrength));
    }

    /// <summary>
    /// 입력 잠금, 대시 및 비활성화에 사용하도록 진행 중인 걸음 주기를 초기화한다.
    /// 착지 이벤트를 발행하지 않고 보행 누적 상태만 제거한다.
    /// </summary>
    private void ResetWalkStep()
    {
        _stepInProgress = false;
        _stepElapsed = 0f;
        _stepDistanceAccumulated = 0f;
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
        _isDodging = false;
        _moveInput = Vector2.zero;
        _pendingLookDelta = Vector2.zero;
        _planarVelocity = Vector3.zero;
        ResetWalkStep();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _iaidoMaxDistance = Mathf.Max(0f, _iaidoMaxDistance);
        _iaidoSpeed = Mathf.Max(0f, _iaidoSpeed);
        _crouchWalkSpeed = Mathf.Max(0f, _crouchWalkSpeed);
        _crouchedCharacterHeight = Mathf.Max(0.1f, _crouchedCharacterHeight);
        _crouchTransitionDuration = Mathf.Max(0.01f, _crouchTransitionDuration);
        _crouchFootstepNoise = Mathf.Clamp01(_crouchFootstepNoise);
    }
#endif
}

