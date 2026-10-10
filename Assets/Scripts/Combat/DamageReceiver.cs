using System;

using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DamageReceiver : MonoBehaviour, IDamageable
{
    [Header("피해 수신 이벤트")]
    [SerializeField] private UnityEvent<DamageContext> _onDamageReceived = new UnityEvent<DamageContext>();

    public event Action<DamageContext> DamageReceived;

    /// <summary>
    /// 활성 수신자가 양수 피해 context를 받으면 C# 및 Inspector 이벤트에 문맥을 전달한다.
    /// 체력은 직접 관리하지 않으며 연결된 수신 기능이나 파생 클래스에서 피해 상태를 변경한다.
    /// </summary>
    public virtual void TakeDamage(DamageContext context)
    {
        if (!isActiveAndEnabled || context.Amount <= 0f)
            return;

        DamageReceived?.Invoke(context);
        _onDamageReceived.Invoke(context);
    }
}
