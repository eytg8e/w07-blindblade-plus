using UnityEngine;


/// <summary>적의 기존 IDamageable 호출을 받아 피해 전달 전에 전방 근접·보스 돌진 방어를 확인한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KatanaCombatCoordinator))]
public sealed class KatanaDamageReceiver : PlayerHealth
{
    [Header("Katana connection")]
    [SerializeField] private KatanaCombatCoordinator _combat;
    [SerializeField] private Transform _view;
    [SerializeField, Range(1f, 180f)] private float _frontArc = 120f;
    [System.NonSerialized] private PlayerMotor _motor;

    protected override void Awake()
    {
        base.Awake();
        _motor = GetComponent<PlayerMotor>();
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        if (_view == null)
        {
            Camera camera = GetComponentInChildren<Camera>();
            _view = camera != null ? camera.transform : transform;
        }
    }

    void OnEnable()
    {
        _motor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// context의 전방 근접·보스 돌진 피해를 먼저 가드·패링으로 검사한다.
    /// 성공하면 피해를 소비하고 실패하면 PlayerHealth가 HP 차감과 사망 및 피해 이벤트를 처리한다.
    /// </summary>
    public override void TakeDamage(DamageContext context)
    {
        if (!isActiveAndEnabled || context.Amount <= 0f || _combat.Player.IsPaused
            || Time.timeScale <= 0f || _combat.Player.CurrentState == PlayerActionState.Dead) return;
        if (_combat.TryDefendIncoming(context, IsFrontMelee(context) || IsFrontBossCharge(context) || IsFrontBossRanged(context))) return;
        base.TakeDamage(context);
    }

    /// <summary>
    /// context의 투사체 진입 방향으로 전방 가드·패링을 검사하고 방어 성공 여부를 반환한다.
    /// parried에 정확한 패링 여부를 출력하며 기존 방어 효과와 반격 기회를 적용한다.
    /// </summary>
    public bool TryDefendProjectile(DamageContext context, out bool parried)
    {
        parried = false;
        if (!isActiveAndEnabled) return false;
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        bool eligible = incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
        bool perfect = _combat.IsParryWindowOpen;
        if (!_combat.TryDefendIncoming(context, eligible)) return false;
        parried = perfect;
        return true;
    }

    /// <summary>context의 공격자가 실행 중인 유일한 패턴이 근접 공격이고 전방 각도 안에 있는지 반환한다.</summary>
    private bool IsFrontMelee(DamageContext context)
    {
        if (context.Instigator == null) return false;
        EnemyAttackBehaviour matching = null;
        foreach (EnemyAttackBehaviour pattern in context.Instigator.GetComponents<EnemyAttackBehaviour>())
        {
            if (pattern.PatternId != context.PatternId) continue;
            if (matching != null) return false;
            matching = pattern;
        }
        if (!(matching is EnemyMeleeAttack) || matching.Phase != EnemyAttackPhase.Active) return false;
        Vector3 toEnemy = Vector3.ProjectOnPlane(context.Instigator.transform.position - transform.position, Vector3.up);
        Vector3 forward = _motor.ViewForward;
        return toEnemy.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f
            && Vector3.Angle(forward, toEnemy) <= _frontArc * 0.5f;
    }

    /// <summary>context가 진행 중인 보스 돌진이고 진입 방향이 전방 방어 각도 안인지 반환한다.</summary>
    private bool IsFrontBossCharge(DamageContext context)
    {
        if (context.PatternId != "BossCharge" || context.Instigator == null) return false;
        BossController boss = context.Instigator.GetComponent<BossController>();
        if (boss == null || (boss.State != BossState.Charging && boss.State != BossState.ChainCharge)) return false;
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        return incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
    }

    /// <summary>보스 원거리 투사체의 진입 방향이 전방 방어 각도 안인지 반환한다. 발사 후 보스 상태와 무관하게 검사한다.</summary>
    private bool IsFrontBossRanged(DamageContext context)
    {
        if (context.PatternId != "BossRanged" || context.Instigator == null
            || context.Instigator.GetComponent<BossController>() == null) return false;
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        return incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
    }

    /// <summary>context의 방향 제한 없는 기본 가드는 사용하지 않는다. 전방 근접·보스 돌진 방어는 Coordinator에서 확정한다.</summary>
    protected override bool CanBlockDamage(DamageContext context)
    {
        return false;
    }
}
