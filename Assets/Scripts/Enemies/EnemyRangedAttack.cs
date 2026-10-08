using UnityEngine;
using UnityEngine.Events;

using BlindBlade.Combat;

namespace BlindBlade.Enemies
{
    public class EnemyRangedAttack : EnemyAttackBehaviour
    {
        [Header("원거리 공격 설정")]
        [SerializeField, Min(0.1f)] private float _attackRange = 8f;
        [SerializeField, Min(0f)] private float _damage = 10f;

        [Header("투사체 연결")]
        [SerializeField] private EnemyProjectile _projectilePrefab;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float _aimHeight = 1f;

        [Header("원거리 공격 이벤트")]
        [SerializeField] private UnityEvent<EnemyAttackContext> _onRangedAttack = new UnityEvent<EnemyAttackContext>();

        public override float Range => _attackRange;

        protected EnemyProjectile ProjectilePrefab => _projectilePrefab;
        protected float Damage => _damage;
        protected float AimHeight => _aimHeight;
        protected Vector3 LaunchPosition => _muzzle != null ? _muzzle.position : transform.TransformPoint(_muzzleOffset);

        /// <summary>
        /// 전조 때 고정한 context의 조준 위치로 연결된 투사체를 발사하고 원거리 이벤트를 전달한다.
        /// 프리팹이 없으면 이벤트만 호출하며 현재 대상 위치로 재조준하지 않는다.
        /// </summary>
        protected override void PerformAttack(EnemyAttackContext context)
        {
            if (_projectilePrefab != null)
            {
                Vector3 origin = LaunchPosition;
                Vector3 direction = (context.AimPosition + Vector3.up * _aimHeight - origin).normalized;
                if (direction.sqrMagnitude > 0f)
                {
                    EnemyProjectile projectile = CreateProjectile(origin, Quaternion.LookRotation(direction));
                    projectile.Initialize(new DamageContext(_damage, gameObject, origin, direction, context.PatternId), direction);
                }
            }

            _onRangedAttack.Invoke(context);
        }

        /// <summary>
        /// position과 rotation을 사용하여 Inspector에 연결한 투사체 프리팹 인스턴스를 반환한다.
        /// 기본 구현은 Instantiate를 사용하며 파생 클래스에서 풀이나 별도 생성기로 교체할 수 있다.
        /// </summary>
        protected virtual EnemyProjectile CreateProjectile(Vector3 position, Quaternion rotation)
        {
            return Instantiate(_projectilePrefab, position, rotation);
        }
    }
}
