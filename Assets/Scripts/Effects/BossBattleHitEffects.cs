using UnityEngine;

/// <summary>보스전의 실제 보스 피해를 접촉점 섬광으로 표현하고 체력 변경을 기록한다.</summary>
[DisallowMultipleComponent]
public sealed class BossBattleHitEffects : MonoBehaviour
{
    [Header("피해 수신자")]
    [SerializeField] private EnemyHealth _bossHealth;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerMotor _playerMotor;
    private EffectManager _effects;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float _previousBossHealth;
    private float _previousPlayerHealth;
#endif

    [Header("보스 적중")]
    [SerializeField] private EffectId _bossHitEffect = EffectId.BossDamageHit;


    void OnEnable()
    {
        _bossHealth.Damaged += HandleBossDamaged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _bossHealth.HealthChanged += HandleBossHealthChanged;
        _playerHealth.HealthChanged += HandlePlayerHealthChanged;
#endif
    }

    void Start()
    {
        _playerHealth.GetComponent<PlayerAttackSlashVfx>().SetUseOutlineShader(true);
        _effects = Game.Instance.EffectManager;
        if (!_effects.IsReady) _effects.Initialize();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _previousBossHealth = _bossHealth.CurrentHealth;
        _previousPlayerHealth = _playerHealth.CurrentHealth;
#endif
    }

    void OnDisable()
    {
        if (_bossHealth != null) _bossHealth.Damaged -= HandleBossDamaged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_bossHealth != null) _bossHealth.HealthChanged -= HandleBossHealthChanged;
        if (_playerHealth != null) _playerHealth.HealthChanged -= HandlePlayerHealthChanged;
#endif
    }

    /// <summary>플레이어가 보스에 가한 실제 피해의 접촉점에 타격 섬광을 재생한다.</summary>
    private void HandleBossDamaged(DamageContext context)
    {
        if (context.Instigator != _playerHealth.gameObject) return;
        _effects.Play(_bossHitEffect, context.Point, Quaternion.LookRotation(-_playerMotor.ViewForward));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>보스 current 체력이 변하면 이전·현재·최대 체력을 기록하고 이전 값을 갱신한다.</summary>
    private void HandleBossHealthChanged(float current, float maximum)
    {
        if (current == _previousBossHealth) return;
        Debug.Log($"[보스 체력] {_previousBossHealth} → {current} / {maximum}", _bossHealth);
        _previousBossHealth = current;
    }

    /// <summary>플레이어 current 체력이 변하면 이전·현재·최대 체력을 기록하고 이전 값을 갱신한다.</summary>
    private void HandlePlayerHealthChanged(float current, float maximum)
    {
        if (current == _previousPlayerHealth) return;
        Debug.Log($"[플레이어 체력] {_previousPlayerHealth} → {current} / {maximum}", _playerHealth);
        _previousPlayerHealth = current;
    }
#endif
}
