public interface IDamageable
{
    /// <summary>
    /// context의 피해량, 공격자와 타격 정보를 사용하여 구현체의 체력 또는 피해 상태를 변경한다.
    /// 반환값 없이 수신자가 피해 적용과 사망 등의 후속 처리를 결정한다.
    /// </summary>
    public void TakeDamage(DamageContext context);
}
