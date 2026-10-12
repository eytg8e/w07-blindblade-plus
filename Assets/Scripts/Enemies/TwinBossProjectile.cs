using System;

using UnityEngine;

/// <summary>쌍둥이 사격의 전방 방어와 발사자를 추적하는 반사 탄환을 처리한다.</summary>
[DisallowMultipleComponent]
public sealed class TwinBossProjectile : MonoBehaviour
{
    [Header("비행 설정")]
    [SerializeField, Min(0.1f)] private float _speed = 12f;
    [SerializeField, Min(0.1f)] private float _lifetime = 5f;
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.12f;
    [SerializeField] private LayerMask _collisionMask = Physics.DefaultRaycastLayers;
    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private readonly Collider[] _contacts = new Collider[32];
    private EnemyHealth _shooter;
    private Collider _shooterCollider;
    private KatanaDamageReceiver _player;
    private DamageContext _damage;
    private Vector3 _direction;
    private float _elapsed;
    private bool _reflected;
    private bool _flying;

    public event Action<TwinBossProjectile> ReflectedHit;
    public event Action<TwinBossProjectile> Released;

    /// <summary>shooter·player·damage·direction으로 발사자와 피해 및 초기 비행 방향을 설정한다.</summary>
    public void Initialize(EnemyHealth shooter, Collider shooterCollider,
        KatanaDamageReceiver player, float damage, Vector3 direction)
    {
        _shooter = shooter;
        _shooterCollider = shooterCollider;
        _player = player;
        _direction = direction.normalized;
        _damage = new DamageContext(damage, shooter.gameObject, transform.position, _direction, "TwinBossRanged");
        _elapsed = 0f;
        _reflected = false;
        _flying = true;
    }

    void Update()
    {
        if (!_flying) return;
        _elapsed += Time.deltaTime;
        if (_elapsed >= _lifetime || _player == null || !_player.IsAlive
            || (_reflected && (_shooter == null || !_shooter.IsAlive)))
        {
            Release();
            return;
        }
        if (_reflected)
        {
            Vector3 toShooter = _shooterCollider.bounds.center - transform.position;
            if (toShooter.sqrMagnitude > 0.000001f) _direction = toShooter.normalized;
        }
        transform.rotation = Quaternion.LookRotation(_direction);
        int overlaps = Physics.OverlapSphereNonAlloc(transform.position, _hitRadius,
            _contacts, _collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps; i++)
        {
            if (!CanHit(_contacts[i])) continue;
            ProcessHit(_contacts[i], _contacts[i].ClosestPoint(transform.position));
            return;
        }
        float distance = _speed * Time.deltaTime;
        int count = Physics.SphereCastNonAlloc(transform.position, _hitRadius, _direction,
            _hits, distance, _collisionMask, QueryTriggerInteraction.Ignore);
        int closest = -1;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!CanHit(_hits[i].collider) || _hits[i].distance >= closestDistance) continue;
            closest = i;
            closestDistance = _hits[i].distance;
        }
        if (closest >= 0)
        {
            transform.position += _direction * closestDistance;
            ProcessHit(_hits[closest].collider, _hits[closest].point);
            return;
        }
        transform.position += _direction * distance;
    }

    /// <summary>collider가 비행 대상 또는 장애물인지 반환하며 다른 적과 발사자 충돌을 제외한다.</summary>
    private bool CanHit(Collider collider)
    {
        if (collider.transform.IsChildOf(transform)) return false;
        EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null) return _reflected && enemy == _shooter;
        PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
        if (player != null) return !_reflected && player == _player;
        return true;
    }

    /// <summary>collider·point에 피해를 적용하고 정확한 패링은 발사자 추적으로 전환하며 적중을 알린다.</summary>
    private void ProcessHit(Collider collider, Vector3 point)
    {
        DamageContext context = new DamageContext(_damage.Amount, _damage.Instigator,
            point, _direction, _damage.PatternId);
        if (!_reflected && collider.GetComponentInParent<KatanaDamageReceiver>() == _player)
        {
            if (_player.TryDefendProjectile(context, out bool parried))
            {
                if (parried && _shooter != null && _shooter.IsAlive)
                {
                    _reflected = true;
                    _elapsed = 0f;
                    _damage = new DamageContext(_damage.Amount, _player.gameObject,
                        point, -_direction, "ReflectedProjectile");
                    return;
                }
                Release();
                return;
            }
        }
        IDamageable receiver = collider.GetComponentInParent<IDamageable>();
        if (receiver != null) receiver.TakeDamage(context);
        if (_reflected && collider.GetComponentInParent<EnemyHealth>() == _shooter)
            ReflectedHit?.Invoke(this);
        Release();
    }

    /// <summary>비행을 종료하고 구독자에게 정리를 알린 뒤 투사체를 파괴한다.</summary>
    public void Release()
    {
        if (!_flying) return;
        _flying = false;
        Released?.Invoke(this);
        Destroy(gameObject);
    }

    void OnDisable()
    {
        if (!_flying) return;
        _flying = false;
        Released?.Invoke(this);
    }
}
