using System;
using System.Collections.Generic;

using UnityEngine;


[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    private static readonly Dictionary<string, EnemyHealth> _targets = new Dictionary<string, EnemyHealth>();

    [Header("적 체력")]
    [SerializeField, Min(1f)] private float _maxHealth = 30f;
    [SerializeField, Tooltip("켜면 양수 피해를 주는 유효한 공격 한 번에 사망합니다.")]
    private bool _dieOnAnyHit = true;
    [SerializeField] private Collider _hitCollider;
    [SerializeField] private bool _cancelAttackOnHit;
    private EnemyAttackSelector _attackSelector;
    private float _currentHealth;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => isActiveAndEnabled && _currentHealth > 0f;
    public string TargetId => "Enemy_" + GetInstanceID();
    public event Action<DamageContext> Damaged;
    public event Action<float, float> HealthChanged;
    public event Action<EnemyHealth> Died;

    void Awake()
    {
        _attackSelector = GetComponent<EnemyAttackSelector>();
    }

    void OnEnable()
    {
        _currentHealth = _maxHealth;
        _targets[TargetId] = this;
    }

    void OnDisable()
    {
        _targets.Remove(TargetId);
    }

    /// <summary>
    /// context의 양수 피해를 현재 HP에 반영하고 한 대 사망 설정이면 HP를 즉시 0으로 만든다.
    /// 설정에 따라 공격을 취소하며 HP가 0이면 사망 이벤트 후 적 루트를 비활성화한다.
    /// </summary>
    public void TakeDamage(DamageContext context)
    {
        if (!IsAlive || !(context.Amount > 0f))
            return;

        _currentHealth = _dieOnAnyHit ? 0f : Mathf.Max(0f, _currentHealth - context.Amount);
        if (_cancelAttackOnHit && _attackSelector != null)
            _attackSelector.CancelCurrentAttack();
        Damaged?.Invoke(context);
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        if (_currentHealth <= 0f)
        {
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 지정된 Collider와 현재 체력 상태로 플레이어 계약의 전투 대상 정보를 반환한다.
    /// 식별자는 인스턴스마다 고유하며 반경은 Collider의 수평 크기를 사용한다.
    /// </summary>
    public PlayerCombatTarget CreateCombatTarget()
    {
        Vector3 extents = _hitCollider.bounds.extents;
        return new PlayerCombatTarget(TargetId, transform.position, _hitCollider,
            Mathf.Max(extents.x, extents.z), IsAlive);
    }

    /// <summary>
    /// targetId로 활성 적 체력 레지스트리를 조회하여 생존 적을 health로 반환한다.
    /// 식별자가 없거나 죽은 적이면 false를 반환한다.
    /// </summary>
    public static bool TryFind(string targetId, out EnemyHealth health)
    {
        health = null;
        return !string.IsNullOrEmpty(targetId) && _targets.TryGetValue(targetId, out health) && health.IsAlive;
    }

    /// <summary>
    /// 플레이 시작 시 입력 없이 이전 활성 적 레지스트리를 비운다.
    /// Domain Reload를 끈 경우에도 이전 실행의 적 참조를 유지하지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        _targets.Clear();
    }
}
