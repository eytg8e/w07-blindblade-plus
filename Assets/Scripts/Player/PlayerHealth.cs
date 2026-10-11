using System;

using UnityEngine;


/// <summary>
/// 공통 피해 수신 계약을 플레이어 가드, 체력 및 사망 상태에 연결한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : DamageReceiver
{
    [Header("플레이어 체력")]
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(0f)] private float _hitRecovery = 0.18f;

    private PlayerController _player;
    private PlayerMotor _motor;
    private float _currentHealth;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => isActiveAndEnabled && _currentHealth > 0f
        && _player.CurrentState != PlayerActionState.Dead;

    public event Action<float, float> HealthChanged;
    public event Action<DamageContext> DamageBlocked;
    public event Action<PlayerHealth> Died;

    protected virtual void Awake()
    {
        _player = GetComponent<PlayerController>();
        _motor = GetComponent<PlayerMotor>();
        _currentHealth = _maxHealth;
    }

    /// <summary>
    /// context의 양수 피해를 가드 상태로 판정하고 막지 못하면 현재 체력에서 차감한다.
    /// 가드는 성공 이벤트를 전달하며 체력이 0이면 플레이어 행동을 사망 상태로 전환한다.
    /// </summary>
    public override void TakeDamage(DamageContext context)
    {
        if (!IsAlive || !(context.Amount > 0f))
            return;

        if (CanBlockDamage(context))
        {
            _player.NotifyGuardSuccess();
            DamageBlocked?.Invoke(context);
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - context.Amount);
        _motor.AddCombatRecoil(context.Direction, 1.4f);
        bool died = _currentHealth <= 0f;
        if (died)
            _player.SetDead();
        else
            _player.NotifyHitReaction(_hitRecovery);

        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        base.TakeDamage(context);
        if (died)
            Died?.Invoke(this);
    }

    /// <summary>context의 피해를 현재 가드로 막을 수 있는지 반환한다. 카타나 수신자는 방향 제한을 별도로 적용한다.</summary>
    protected virtual bool CanBlockDamage(DamageContext context)
    {
        return _player.TryBlockIncomingAttack();
    }
}
