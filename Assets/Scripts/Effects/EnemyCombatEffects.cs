using UnityEngine;

using BlindBlade.Enemies;

/// <summary>
/// 실제 적 공격의 실행 이벤트를 구독하여 EnemySlash를 요청한다.
/// 전조·피해·이동 판정은 기존 적 시스템에 맡긴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyCombatEffects : MonoBehaviour
{
    [Header("공격 표시 위치")]
    [SerializeField] private float _height = 1f;
    [SerializeField, Min(0f)] private float _forwardOffset = 0.35f;
    private EnemyAttackBehaviour[] _patterns;
    private EffectManager _effects;

    void Awake()
    {
        _patterns = GetComponents<EnemyAttackBehaviour>();
    }

    void OnEnable()
    {
        foreach (EnemyAttackBehaviour pattern in _patterns)
            pattern.AttackCommitted += HandleAttack;
    }

    void Start()
    {
        _effects = Game.Instance.EffectManager;
    }

    void OnDisable()
    {
        foreach (EnemyAttackBehaviour pattern in _patterns)
            pattern.AttackCommitted -= HandleAttack;
    }

    /// <summary>
    /// context의 실제 공격 실행 위치와 고정 방향으로 EnemySlash를 배치한다.
    /// 실행 이벤트마다 단발 효과만 요청하고 피해나 공격 상태는 변경하지 않는다.
    /// </summary>
    private void HandleAttack(EnemyAttackContext context)
    {
        if (_effects == null || !_effects.IsReady)
            return;
        Vector3 direction = Vector3.ProjectOnPlane(context.Direction, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();
        Vector3 position = transform.position + Vector3.up * _height + direction * _forwardOffset;
        _effects.Play(EffectId.EnemySlash, position, Quaternion.LookRotation(direction, Vector3.up));
    }
}
