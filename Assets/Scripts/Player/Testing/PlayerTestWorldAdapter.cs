using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test-only stand-in for perception, enemy combat, and map services.
/// Use only in a player sandbox scene; replace it with the owning team adapters in gameplay scenes.
/// </summary>
public sealed class PlayerTestWorldAdapter : MonoBehaviour, IPlayerPerceptionProvider, IPlayerCombatService, IPlayerMapService
{
    [Header("Test _targets treated as currently sound-detected")]
    [SerializeField] private PlayerTestTarget[] _targets;
    [SerializeField, Min(0f)] private float _detectionMemory = 1.5f;
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private LayerMask _attackMask = ~0;

    private readonly List<PlayerPerceivedTarget> _perceivedTargets = new List<PlayerPerceivedTarget>();

    public PlayerSenseContext LastSenseContext { get; private set; }

    /// <summary>플레이어의 감각 상태를 테스트 어댑터 내부에 저장합니다.</summary>
    public void SetPlayerSenseContext(PlayerSenseContext context)
    {
        LastSenseContext = context;
    }

    /// <summary>테스트 대상 목록을 현재 감지 대상으로 변환해 반환합니다.</summary>
    public IReadOnlyList<PlayerPerceivedTarget> GetCurrentTargets()
    {
        _perceivedTargets.Clear();
        if (_targets == null)
            return _perceivedTargets;

        float now = Time.time;
        foreach (PlayerTestTarget target in _targets)
        {
            if (target == null || !target.IsAlive)
                continue;

            _perceivedTargets.Add(new PlayerPerceivedTarget(
                target.TargetId,
                target.WorldPosition,
                now,
                now + _detectionMemory));
        }

        return _perceivedTargets;
    }

    /// <summary>식별자 또는 충돌체에 대응하는 생존 테스트 대상의 전투 정보를 반환합니다.</summary>
    public bool TryGetTarget(string targetId, out PlayerCombatTarget target)
    {
        if (_targets != null)
        {
            foreach (PlayerTestTarget candidate in _targets)
            {
                if (candidate == null || !candidate.IsAlive || candidate.TargetId != targetId)
                    continue;

                target = candidate.CreateCombatTarget();
                return true;
            }
        }

        target = default;
        return false;
    }

    /// <summary>식별자 또는 충돌체에 대응하는 생존 테스트 대상의 전투 정보를 반환합니다.</summary>
    public bool TryGetTarget(Collider collider, out PlayerCombatTarget target)
    {
        PlayerTestTarget testTarget = collider != null ? collider.GetComponentInParent<PlayerTestTarget>() : null;
        if (testTarget != null && testTarget.IsAlive)
        {
            target = testTarget.CreateCombatTarget();
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>광선 판정으로 공격 대상을 확인하고 발도 적중이면 테스트 대상 사망을 확정해 결과를 반환합니다.</summary>
    public PlayerAttackResolution ResolvePlayerAttack(PlayerAttackRequest request)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            request.Origin,
            request.Direction,
            request.Range,
            _attackMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsPlayerCollider(hit.collider))
                continue;

            PlayerTestTarget target = hit.collider.GetComponentInParent<PlayerTestTarget>();
            if (target == null || !target.IsAlive)
                return new PlayerAttackResolution(request.ActionId, false, null, false);

            if (!string.IsNullOrEmpty(request.PreferredTargetId)
                && target.TargetId != request.PreferredTargetId)
            {
                return new PlayerAttackResolution(request.ActionId, false, null, false);
            }

            bool isFinisher = request.Kind == PlayerAttackKind.IaidoFinisher;
            if (isFinisher)
                target.ConfirmIaidoDeath();

            return new PlayerAttackResolution(request.ActionId, true, target.TargetId, isFinisher);
        }

        return new PlayerAttackResolution(request.ActionId, false, null, false);
    }

    /// <summary>충돌한 테스트 벽에 파괴를 요청하고 파괴·통과 결과를 반환합니다.</summary>
    public PlayerWallResolution ResolveDashWallContact(Collider wallCollider, Vector3 contactPoint, Vector3 contactNormal)
    {
        if (wallCollider == null)
            return new PlayerWallResolution(false, false, false);

        PlayerTestWall wall = wallCollider.GetComponentInParent<PlayerTestWall>();
        if (wall == null)
            return new PlayerWallResolution(false, false, false);

        return wall.TryBreak(wallCollider);
    }

    /// <summary>후보 충돌체가 지정된 테스트 플레이어의 계층에 속하는지 반환합니다.</summary>
    private bool IsPlayerCollider(Collider candidate)
    {
        return _playerRoot != null
            && (candidate.transform == _playerRoot || candidate.transform.IsChildOf(_playerRoot));
    }
}
