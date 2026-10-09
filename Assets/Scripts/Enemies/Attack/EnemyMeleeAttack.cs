using System;
using System.Collections.Generic;

using UnityEngine;

using BlindBlade.Combat;

namespace BlindBlade.Enemies
{
    public class EnemyMeleeAttack : EnemyAttackBehaviour
    {
        [Header("근접 공격 설정")]
        [SerializeField, Min(0.1f)] private float _attackRange = 2f;
        [SerializeField, Min(0f)] private float _damage = 10f;

        [Header("근접 타격 설정")]
        [SerializeField] private bool _useBuiltInHitDetection = true;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.75f;
        [SerializeField] private float _hitHeight = 1f;
        [SerializeField] private LayerMask _hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;

        public override float Range => _attackRange;
        public event Action<EnemyAttackContext> MeleeAttack;

        protected override EnemySoundAction SoundAction => EnemySoundAction.Melee;

        protected float Damage => _damage;
        protected float HitRadius => _hitRadius;
        protected float HitHeight => _hitHeight;
        protected LayerMask HitMask => _hitMask;

        /// <summary>
        /// 전조 때 고정한 context의 조준 위치에서 근접 타격을 처리하고 공격 이벤트를 전달한다.
        /// 내장 판정을 끄면 이벤트만 호출하여 외부 무기나 애니메이션 기반 판정으로 교체할 수 있다.
        /// </summary>
        protected override void PerformAttack(EnemyAttackContext context)
        {
            if (_useBuiltInHitDetection)
                ApplyHit(context);
            MeleeAttack?.Invoke(context);
        }

        /// <summary>
        /// context의 고정 조준 위치와 Inspector의 반경·레이어로 Collider를 수집하여 피해를 전달한다.
        /// 지정 대상 계층만 판정하고 벽 뒤의 수신자를 제외하여 공격 한 번당 한 번만 피해를 적용한다.
        /// </summary>
        protected virtual void ApplyHit(EnemyAttackContext context)
        {
            Vector3 center = context.AimPosition + Vector3.up * _hitHeight;
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();
            foreach (Collider collider in Physics.OverlapSphere(center, _hitRadius, _hitMask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(transform) || context.Target == null ||
                    !collider.transform.IsChildOf(context.Target))
                    continue;

                IDamageable receiver = collider.GetComponentInParent<IDamageable>();
                if (receiver == null || damaged.Contains(receiver))
                    continue;

                Vector3 point = collider.ClosestPoint(center);
                if (IsBlocked(context, point, receiver))
                    continue;

                damaged.Add(receiver);
                receiver.TakeDamage(new DamageContext(_damage, gameObject, point, context.Direction, context.PatternId));
            }
        }

        /// <summary>
        /// context의 시작 위치에서 point까지 장애물 레이어를 검사한다.
        /// 자신과 receiver의 Collider를 제외한 차폐물이 있으면 true를 반환한다.
        /// </summary>
        protected virtual bool IsBlocked(EnemyAttackContext context, Vector3 point, IDamageable receiver)
        {
            Vector3 origin = context.Origin + Vector3.up * _hitHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, point - origin, Vector3.Distance(origin, point),
                _obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<IDamageable>() == receiver)
                    continue;
                return true;
            }

            return false;
        }
    }
}
