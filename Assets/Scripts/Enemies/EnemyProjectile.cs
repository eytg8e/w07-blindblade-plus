using System;

using UnityEngine;


public class EnemyProjectile : MonoBehaviour
{
    [Header("투사체 설정")]
    [SerializeField, Min(0.1f)] private float _speed = 12f;
    [SerializeField, Min(0.01f)] private float _lifetime = 4f;
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.12f;
    [SerializeField] private LayerMask _collisionMask = Physics.DefaultRaycastLayers;
    private DamageContext _damage;
    private Transform _target;
    private Vector3 _direction;
    private float _elapsedTime;
    private bool _isFlying;

    [Header("공격 판정 기즈모")]
    [SerializeField] private bool _showAttackGizmos = true;
#if UNITY_EDITOR
    private Vector3 _debugStart;
    private Vector3 _debugEnd;
    private bool _hasDebugSegment;
#endif

    public event Action<Collider, DamageContext> Hit;
    public event Action<Vector3> Impact;

    protected DamageContext Damage => _damage;
    protected Vector3 FlightDirection => _direction;

    void Update()
    {
        if (!_isFlying)
            return;

        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= _lifetime)
        {
            Release();
            return;
        }

#if UNITY_EDITOR
        _debugStart = transform.position;
        _debugEnd = transform.position + _direction * (_speed * Time.deltaTime);
        _hasDebugSegment = true;
#endif
        foreach (Collider overlap in Physics.OverlapSphere(transform.position, _hitRadius,
            _collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (!CanHit(overlap))
                continue;

#if UNITY_EDITOR
            _debugEnd = _debugStart;
#endif
            ProcessHit(overlap, overlap.ClosestPoint(transform.position));
            return;
        }

        float distance = _speed * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, _hitRadius, _direction,
            distance, _collisionMask, QueryTriggerInteraction.Ignore);
        int closestIndex = -1;
        float closestDistance = float.PositiveInfinity;
        for (int index = 0; index < hits.Length; index++)
        {
            if (!CanHit(hits[index].collider) || hits[index].distance >= closestDistance)
                continue;

            closestIndex = index;
            closestDistance = hits[index].distance;
        }

        if (closestIndex >= 0)
        {
            RaycastHit hit = hits[closestIndex];
            transform.position += _direction * hit.distance;
#if UNITY_EDITOR
            _debugEnd = transform.position;
#endif
            ProcessHit(hit.collider, hit.point);
            return;
        }

        transform.position += _direction * distance;
    }

    void OnDisable()
    {
        _isFlying = false;
    }

    /// <summary>
    /// damage와 direction 및 target을 저장하고 이동 방향, 수명 누적 및 비행 상태를 초기화한다.
    /// 풀에서 재사용하는 경우에도 호출하며 고정된 방향으로 이동을 시작한다.
    /// </summary>
    public virtual void Initialize(DamageContext damage, Vector3 direction, Transform target = null)
    {
        _damage = damage;
        _target = target;
        _direction = direction.normalized;
        _elapsedTime = 0f;
#if UNITY_EDITOR
        _hasDebugSegment = false;
#endif
        _isFlying = _direction.sqrMagnitude > 0f;
        if (_isFlying)
            transform.rotation = Quaternion.LookRotation(_direction);
        else
            Release();
    }

    /// <summary>
    /// collider가 자신·공격자·다른 적 또는 지정 대상 밖의 피해 수신자인지 검사한다.
    /// 제외 대상이면 false를 반환하며 지정 대상과 벽의 충돌은 허용한다.
    /// </summary>
    protected virtual bool CanHit(Collider collider)
    {
        return !collider.transform.IsChildOf(transform) &&
            (_damage.Instigator == null || !collider.transform.IsChildOf(_damage.Instigator.transform)) &&
            collider.GetComponentInParent<EnemyHealth>() == null &&
            (_target == null || collider.GetComponentInParent<IDamageable>() == null || collider.transform.IsChildOf(_target));
    }

    /// <summary>
    /// collider의 피해 수신자에게 point와 저장한 피해 정보를 전달하고 충돌 이벤트를 발생시킨다.
    /// 기본 구현은 최초 충돌에서 비행을 중지하고 투사체를 반환한다.
    /// </summary>
    protected virtual void ProcessHit(Collider collider, Vector3 point)
    {
        _isFlying = false;
        DamageContext damage = new DamageContext(_damage.Amount, _damage.Instigator,
            point, _direction, _damage.PatternId);
        IDamageable receiver = collider.GetComponentInParent<IDamageable>();
        receiver?.TakeDamage(damage);
        Hit?.Invoke(collider, damage);
        Impact?.Invoke(point);
        Release();
    }

    /// <summary>
    /// 비행 상태를 종료하고 기본 구현에서는 투사체 GameObject를 파괴한다.
    /// 입력값 없이 정리하며 파생 클래스에서 풀 반환 방식으로 교체할 수 있다.
    /// </summary>
    public virtual void Release()
    {
        _isFlying = false;
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!_showAttackGizmos || !isActiveAndEnabled || !Application.isPlaying || !_hasDebugSegment) return;
        EnemyCombatService.DrawSweepGizmo(_debugStart, _debugEnd, _hitRadius, Color.magenta);
        UnityEditor.Handles.Label(_debugEnd, "적 투사체: 현재 프레임 검사 구간");
    }

    void OnDrawGizmosSelected()
    {
        if (!_showAttackGizmos || Application.isPlaying) return;
        EnemyCombatService.DrawSweepGizmo(transform.position, transform.position, _hitRadius, Color.yellow);
        UnityEditor.Handles.Label(transform.position, "적 투사체: 판정 반경 미리보기");
    }
#endif
}
