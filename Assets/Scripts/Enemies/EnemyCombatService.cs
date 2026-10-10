using System;

using UnityEngine;

using BlindBlade.Combat;

namespace BlindBlade.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyCombatService : MonoBehaviour, IPlayerCombatService
    {
        [Header("플레이어 공격 판정")]
        [SerializeField] private Transform _playerRoot;
        [SerializeField, Min(0f)] private float _normalDamage = 10f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.2f;
        [SerializeField] private LayerMask _attackMask = ~0;
        private bool _hasResolution;
        private PlayerAttackResolution _lastResolution;

        /// <summary>
        /// 새 공격 판정이 끝나면 요청, 최초 접촉체, 접촉 위치, 실제 적 피해 여부를 알린다.
        /// 접촉이 없으면 Collider는 null이며 동일 ActionId의 캐시 조회에는 다시 알리지 않는다.
        /// </summary>
        public event Action<PlayerAttackRequest, Collider, Vector3, bool> AttackContactResolved;

        void OnEnable()
        {
            _hasResolution = false;
        }

        /// <summary>
        /// targetId로 같은 씬의 생존 적을 찾아 플레이어 전투 대상 정보를 target으로 반환한다.
        /// 등록되지 않은 식별자나 다른 씬의 적이면 false를 반환한다.
        /// </summary>
        public bool TryGetTarget(string targetId, out PlayerCombatTarget target)
        {
            if (EnemyHealth.TryFind(targetId, out EnemyHealth health) && health.gameObject.scene == gameObject.scene)
            {
                target = health.CreateCombatTarget();
                return true;
            }
            target = default;
            return false;
        }

        /// <summary>
        /// collider의 부모 적 체력을 조회하여 같은 씬의 생존 적이면 target을 반환한다.
        /// 자식 Collider도 루트의 동일한 전투 대상으로 연결하며 유효하지 않으면 false를 반환한다.
        /// </summary>
        public bool TryGetTarget(Collider collider, out PlayerCombatTarget target)
        {
            EnemyHealth health = collider != null ? collider.GetComponentInParent<EnemyHealth>() : null;
            if (health != null && health.IsAlive && health.gameObject.scene == gameObject.scene)
            {
                target = health.CreateCombatTarget();
                return true;
            }
            target = default;
            return false;
        }

        /// <summary>
        /// request의 원점·방향·사거리로 가장 가까운 적 또는 장애물을 검사하여 피해·사망 결과를 반환한다.
        /// 일반 공격은 설정 피해를 적용하고 발도는 남은 HP를 소진하며 같은 ActionId는 중복 적용하지 않는다.
        /// 새 판정의 접촉체·위치·적 피해 여부는 AttackContactResolved로 구독자에게 알린다.
        /// </summary>
        public PlayerAttackResolution ResolvePlayerAttack(PlayerAttackRequest request)
        {
            if (_hasResolution && _lastResolution.ActionId == request.ActionId)
                return _lastResolution;

            PlayerAttackResolution result = new PlayerAttackResolution(request.ActionId, false, null, false);
            Collider collider = null;
            Vector3 point = request.Origin;
            if (isActiveAndEnabled && request.Range > 0f && request.Direction.sqrMagnitude > 0f)
            {
                collider = FindFirstContact(request, out point);
                EnemyHealth health = collider != null ? collider.GetComponentInParent<EnemyHealth>() : null;
                if (health != null && health.IsAlive && health.gameObject.scene == gameObject.scene &&
                    (string.IsNullOrEmpty(request.PreferredTargetId) || request.PreferredTargetId == health.TargetId))
                {
                    string id = health.TargetId;
                    float damage = request.Kind == PlayerAttackKind.IaidoFinisher ? health.CurrentHealth : _normalDamage;
                    health.TakeDamage(new DamageContext(damage, _playerRoot.gameObject, point, request.Direction, request.Kind.ToString()));
                    result = new PlayerAttackResolution(request.ActionId, true, id, health.CurrentHealth <= 0f);
                }
            }
            _hasResolution = true;
            _lastResolution = result;
            AttackContactResolved?.Invoke(request, collider, point, result.HitConfirmed);
            return result;
        }

        /// <summary>
        /// request의 시작점 겹침과 이동 구간을 검사하여 자기 Collider를 제외한 최초 접촉체와 point를 반환한다.
        /// 벽도 후보에 포함하여 벽 뒤의 적에게 피해가 전달되지 않게 하며 없으면 null을 반환한다.
        /// </summary>
        private Collider FindFirstContact(PlayerAttackRequest request, out Vector3 point)
        {
            point = request.Origin;
            Collider closestOverlap = null;
            float overlapDistance = float.PositiveInfinity;
            foreach (Collider overlap in Physics.OverlapSphere(request.Origin, _hitRadius, _attackMask, QueryTriggerInteraction.Ignore))
            {
                if (overlap.transform.IsChildOf(_playerRoot))
                    continue;
                Vector3 overlapPoint = overlap.ClosestPoint(request.Origin);
                if (overlap.GetComponentInParent<EnemyHealth>() == null)
                {
                    point = overlapPoint;
                    return overlap;
                }
                float distance = (overlapPoint - request.Origin).sqrMagnitude;
                if (distance < overlapDistance)
                {
                    closestOverlap = overlap;
                    overlapDistance = distance;
                    point = overlapPoint;
                }
            }
            if (closestOverlap != null)
                return closestOverlap;

            RaycastHit[] hits = Physics.SphereCastAll(request.Origin, _hitRadius, request.Direction,
                request.Range, _attackMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(_playerRoot))
                    continue;
                point = hit.point;
                return hit.collider;
            }
            return null;
        }
    }
}
