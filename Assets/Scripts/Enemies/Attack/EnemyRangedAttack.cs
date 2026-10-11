using System;

using UnityEngine;


public class EnemyRangedAttack : EnemyAttackBehaviour
{
    [Header("원거리 공격 설정")]
    [SerializeField, Min(0.1f)] private float _attackRange = 8f;
    [SerializeField, Min(0f)] private float _damage = 10f;

    [Header("투사체 연결")]
    [SerializeField] private bool _useBuiltInProjectile = true;
    [SerializeField] private EnemyProjectile _projectilePrefab;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _aimHeight = 1f;

    public override float Range => _attackRange;
    public event Action<EnemyAttackContext> RangedAttack;

    protected override EnemySoundAction SoundAction => EnemySoundAction.Ranged;

    protected EnemyProjectile ProjectilePrefab => _projectilePrefab;
    protected float Damage => _damage;
    protected float AimHeight => _aimHeight;
    protected Vector3 LaunchPosition => _muzzle.position;

    /// <summary>
    /// target의 사거리와 명시적인 발사 설정을 검사하여 공격 가능한 경우 true를 반환한다.
    /// 내장 발사가 켜져 있으면 프리팹과 발사점 연결이 모두 있어야 공격을 허용한다.
    /// </summary>
    public override bool CanAttack(Transform target)
    {
        return (!_useBuiltInProjectile || (_projectilePrefab != null && _muzzle != null)) &&
            base.CanAttack(target);
    }

    /// <summary>
    /// 전조 때 고정한 context의 조준 위치로 연결된 투사체를 발사하고 원거리 이벤트를 전달한다.
    /// Use Built In Projectile이 켜져 있으면 연결된 프리팹을 사용하며 꺼져 있으면 이벤트만 호출한다.
    /// </summary>
    protected override void PerformAttack(EnemyAttackContext context)
    {
        if (_useBuiltInProjectile)
        {
            Vector3 origin = LaunchPosition;
            Vector3 direction = (context.AimPosition + Vector3.up * _aimHeight - origin).normalized;
            if (direction.sqrMagnitude > 0f)
            {
                EnemyProjectile projectile = CreateProjectile(origin, Quaternion.LookRotation(direction));
                projectile.Initialize(new DamageContext(_damage, gameObject, origin, direction, context.PatternId), direction, context.Target);
            }
        }

        RangedAttack?.Invoke(context);
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
