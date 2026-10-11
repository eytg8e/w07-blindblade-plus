using UnityEngine;


/// <summary>적의 기존 IDamageable 호출을 받아 피해 전달 전에 전방 근접 쳐내기를 확인한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KatanaCombatCoordinator))]
public sealed class KatanaDamageReceiver : PlayerHealth
{
    [Header("Katana connection")]
    [SerializeField] private KatanaCombatCoordinator _combat;
    [SerializeField] private Transform _view;
    [SerializeField, Range(1f, 180f)] private float _frontArc = 120f;

    protected override void Awake()
    {
        base.Awake();
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        if (_view == null)
        {
            Camera camera = GetComponentInChildren<Camera>();
            _view = camera != null ? camera.transform : transform;
        }
    }

    /// <summary>
    /// context의 전방 근접 피해를 먼저 패링으로 검사한다.
    /// 성공하면 피해를 소비하고 실패하면 PlayerHealth가 HP 차감과 사망 및 피해 이벤트를 처리한다.
    /// </summary>
    public override void TakeDamage(DamageContext context)
    {
        if (!isActiveAndEnabled || context.Amount <= 0f || _combat.Player.IsPaused
            || Time.timeScale <= 0f || _combat.Player.CurrentState == PlayerActionState.Dead) return;
        if (_combat.TryDefendIncoming(context, IsFrontMelee(context))) return;
        base.TakeDamage(context);
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
        Vector3 forward = Vector3.ProjectOnPlane(_combat.Player.GetComponent<PlayerMotor>().ViewForward, Vector3.up);
        return toEnemy.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f
            && Vector3.Angle(forward, toEnemy) <= _frontArc * 0.5f;
    }

    /// <summary>context의 방향 제한 없는 기본 가드는 사용하지 않는다. 전방 근접 방어는 Coordinator에서 확정한다.</summary>
    protected override bool CanBlockDamage(DamageContext context)
    {
        return false;
    }
}
