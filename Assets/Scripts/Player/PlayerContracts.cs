using System.Collections.Generic;
using UnityEngine;

/// <summary>Player actions that can be observed by the game and presentation systems.</summary>
public enum PlayerActionState
{
    Free,
    Guarding,
    Charging,
    NormalAttack,
    IaidoDash,
    IaidoSlash,
    Recovery,
    Paused,
    Dead
}

public enum PlayerLocomotionMode
{
    Stopped,
    Walking,
    GuardWalking,
    ChargingStill,
    ChargingWalking,
    Crouching,
    CrouchWalking,
    IaidoDash
}

public enum PlayerFootstepType
{
    Normal,
    Quiet
}

public enum PlayerAttackKind
{
    Normal,
    IaidoFinisher
}

public enum PlayerAttackOutcome
{
    None,
    Hit,
    Miss,
    WallBlocked
}

public enum PlayerActionStage
{
    StateChanged,
    ChargeStarted,
    ChargeReady,
    ChargeCancelled,
    ChargeEnded,
    GuardStarted,
    GuardEnded,
    GuardSucceeded,
    AttackStarted,
    DashStarted,
    DashArrived,
    DashBlocked,
    SlashStarted,
    HitConfirmed,
    Missed,
    DeathConfirmed,
    ActionEnded,
    RecoveryStarted,
    RecoveryEnded,
    RequestRejected
}

/// <summary>
/// Immutable sensory context sent by the player to perception systems.
/// </summary>
public struct PlayerSenseContext
{
    public bool IsMoving { get; }
    public bool IsFocusing { get; }
    public bool IsCharging { get; }
    public PlayerLocomotionMode LocomotionMode { get; }

    /// <summary>이동, 집중, 충전 상태와 보행 유형으로 감각 문맥 데이터를 생성합니다.</summary>
    public PlayerSenseContext(bool isMoving, bool isFocusing, bool isCharging, PlayerLocomotionMode locomotionMode)
    {
        IsMoving = isMoving;
        IsFocusing = isFocusing;
        IsCharging = isCharging;
        LocomotionMode = locomotionMode;
    }
}

/// <summary>
/// A sound-detected target. ValidUntil uses Unity scaled game time.
/// </summary>
public struct PlayerPerceivedTarget
{
    public string TargetId { get; }
    public Vector3 WorldPosition { get; }
    public float LastHeardTime { get; }
    public float ValidUntil { get; }

    /// <summary>식별자, 감지 위치, 감지 시점, 유효 종료 시각으로 감지 대상 데이터를 생성합니다.</summary>
    public PlayerPerceivedTarget(string targetId, Vector3 worldPosition, float lastHeardTime, float validUntil)
    {
        TargetId = targetId;
        WorldPosition = worldPosition;
        LastHeardTime = lastHeardTime;
        ValidUntil = validUntil;
    }
}

/// <summary>
/// Live combat geometry resolved from a perceived target by the enemy system.
/// </summary>
public struct PlayerCombatTarget
{
    public string TargetId { get; }
    public Vector3 WorldPosition { get; }
    public Collider HitCollider { get; }
    public float CollisionRadius { get; }
    public bool IsAlive { get; }

    /// <summary>적 식별자, 위치, 충돌체, 반경, 생존 상태로 전투 대상 데이터를 생성합니다.</summary>
    public PlayerCombatTarget(string targetId, Vector3 worldPosition, Collider hitCollider, float collisionRadius, bool isAlive)
    {
        TargetId = targetId;
        WorldPosition = worldPosition;
        HitCollider = hitCollider;
        CollisionRadius = Mathf.Max(0f, collisionRadius);
        IsAlive = isAlive;
    }
}

/// <summary>Data published when an actual player footstep occurs.</summary>
public struct PlayerFootstepEvent
{
    public Vector3 WorldPosition { get; }
    public float Time { get; }
    public PlayerFootstepType Type { get; }
    public float HorizontalSpeed { get; }
    public float NoiseStrength { get; }

    /// <summary>발걸음 위치, 시각, 유형, 속도, 소음 강도를 불변 이벤트 데이터로 생성합니다.</summary>
    public PlayerFootstepEvent(Vector3 worldPosition, float time, PlayerFootstepType type, float horizontalSpeed, float noiseStrength)
    {
        WorldPosition = worldPosition;
        Time = time;
        Type = type;
        HorizontalSpeed = horizontalSpeed;
        NoiseStrength = Mathf.Clamp01(noiseStrength);
    }
}

/// <summary>One attack attempt sent to the enemy combat owner for authoritative hit resolution.</summary>
public struct PlayerAttackRequest
{
    public int ActionId { get; }
    public PlayerAttackKind Kind { get; }
    public string PreferredTargetId { get; }
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public float Range { get; }
    public float IssuedAt { get; }

    /// <summary>행동 식별자와 공격 종류, 대상, 원점, 방향, 거리, 시각으로 판정 요청을 생성합니다.</summary>
    public PlayerAttackRequest(int actionId, PlayerAttackKind kind, string preferredTargetId, Vector3 origin, Vector3 direction, float range, float issuedAt)
    {
        ActionId = actionId;
        Kind = kind;
        PreferredTargetId = preferredTargetId;
        Origin = origin;
        Direction = direction.normalized;
        Range = Mathf.Max(0f, range);
        IssuedAt = issuedAt;
    }
}

/// <summary>Enemy-owned result for one attack request.</summary>
public struct PlayerAttackResolution
{
    public int ActionId { get; }
    public bool HitConfirmed { get; }
    public string HitTargetId { get; }
    public bool DeathConfirmed { get; }
    public bool IsComplete { get; }

    /// <summary>actionId·적중 대상·사망 및 isComplete로 피해 판정 결과를 생성한다. 미완료 궤적 검사를 구분한다.</summary>
    public PlayerAttackResolution(int actionId, bool hitConfirmed, string hitTargetId, bool deathConfirmed, bool isComplete = true)
    {
        ActionId = actionId;
        HitConfirmed = hitConfirmed;
        HitTargetId = hitTargetId;
        DeathConfirmed = deathConfirmed;
        IsComplete = isComplete;
    }
}

/// <summary>Map-owned response to a wall contact during iaido travel.</summary>
public struct PlayerWallResolution
{
    public bool IsDestructible { get; }
    public bool BreakSucceeded { get; }
    public bool IsPassable { get; }

    /// <summary>파괴 가능 여부, 파괴 성공 여부, 통과 여부로 맵 충돌 결과를 생성합니다.</summary>
    public PlayerWallResolution(bool isDestructible, bool breakSucceeded, bool isPassable)
    {
        IsDestructible = isDestructible;
        BreakSucceeded = breakSucceeded;
        IsPassable = isPassable;
    }

    public bool AllowsDashContinuation => IsDestructible && BreakSucceeded && IsPassable;
}

/// <summary>One action-stage notification for gameplay, animation, effects, sound, and UI consumers.</summary>
public struct PlayerActionEvent
{
    public int ActionId { get; }
    public PlayerActionState State { get; }
    public PlayerActionStage Stage { get; }
    public PlayerAttackOutcome Outcome { get; }
    public string TargetId { get; }
    public Vector3 WorldPosition { get; }

    /// <summary>행동 식별자, 상태, 단계, 결과, 대상, 위치로 외부 구독용 이벤트를 생성합니다.</summary>
    public PlayerActionEvent(int actionId, PlayerActionState state, PlayerActionStage stage, PlayerAttackOutcome outcome, string targetId, Vector3 worldPosition)
    {
        ActionId = actionId;
        State = state;
        Stage = stage;
        Outcome = outcome;
        TargetId = targetId;
        WorldPosition = worldPosition;
    }
}

/// <summary>
/// The only player-facing contracts required from unfinished team systems.
/// Implementations belong to those systems, not to the Player folder.
/// </summary>
public interface IPlayerPerceptionProvider
{
    /// <summary>현재 유효한 청각 감지 대상을 반환합니다.</summary>
    IReadOnlyList<PlayerPerceivedTarget> GetCurrentTargets();
    /// <summary>플레이어의 이동·집중·충전 문맥을 사운드 감지 시스템에 전달합니다.</summary>
    void SetPlayerSenseContext(PlayerSenseContext context);
}

public interface IPlayerCombatService
{
    /// <summary>식별자 또는 충돌체에 대응하는 적의 전투 정보를 반환합니다.</summary>
    bool TryGetTarget(string targetId, out PlayerCombatTarget target);
    /// <summary>식별자 또는 충돌체에 대응하는 적의 전투 정보를 반환합니다.</summary>
    bool TryGetTarget(Collider collider, out PlayerCombatTarget target);
    /// <summary>공격 요청을 판정하고 적중·즉사 결과를 행동 식별자와 함께 반환합니다.</summary>
    PlayerAttackResolution ResolvePlayerAttack(PlayerAttackRequest request);
}

public interface IPlayerMapService
{
    /// <summary>대시 충돌 대상에 대한 파괴 요청 결과와 실제 통과 가능 여부를 반환합니다.</summary>
    PlayerWallResolution ResolveDashWallContact(Collider wall, Vector3 contactPoint, Vector3 contactNormal);
}
