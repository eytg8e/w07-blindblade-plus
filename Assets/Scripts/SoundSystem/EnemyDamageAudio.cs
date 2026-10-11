using UnityEngine;


/// <summary>
/// 실제 적 피해 이벤트의 접촉 위치에 검 피격음을 한 번 재생한다.
/// 피해와 사망 판정은 적 시스템에 맡기며, 사망 후에도 소리가 자연 종료하도록 공격자를 소유자로 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyDamageAudio : MonoBehaviour
{
    [Header("Damage Sound")]
    [SerializeField] private SoundId _damageSound = SoundId.SwordHit;
    [SerializeField, Min(0f)] private float _volume = 0.8f;
    private EnemyHealth _health;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    void OnEnable()
    {
        _health.Damaged += HandleDamage;
    }

    void OnDisable()
    {
        _health.Damaged -= HandleDamage;
    }

    /// <summary>
    /// context의 검 공격자와 피해 접촉 위치를 사용해 실제 피격음을 재생한다.
    /// 플레이어가 가한 피해만 처리하며 적이 즉시 비활성화되어도 피격음의 끝을 유지한다.
    /// </summary>
    private void HandleDamage(DamageContext context)
    {
        if (context.Instigator == null || context.Instigator.GetComponent<PlayerController>() == null)
            return;
        Managers.Sound.Play(_damageSound, context.Point, _volume, context.Instigator);
    }
}
