using UnityEngine;

/// <summary>보스전의 실제 피해 이벤트를 접촉점 섬광과 플레이어 시야 앞 혈흔으로 표현한다.</summary>
[DisallowMultipleComponent]
public sealed class BossBattleHitEffects : MonoBehaviour
{
    [Header("피해 수신자")]
    [SerializeField] private EnemyHealth _bossHealth;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerMotor _playerMotor;
    private EffectManager _effects;

    [Header("보스 적중")]
    [SerializeField] private EffectId _bossHitEffect = EffectId.BossDamageHit;

    [Header("플레이어 피격")]
    [SerializeField] private EffectId _playerHitEffect = EffectId.PlayerDamageHit;
    [SerializeField, Min(0.1f), Tooltip("카메라 앞 혈흔 표시 거리(미터)")]
    private float _playerEffectDistance = 0.8f;
    [SerializeField, Tooltip("시야 중심에서 혈흔을 아래로 내리는 거리(미터)")]
    private float _playerEffectVerticalOffset = -0.1f;

    void OnEnable()
    {
        _bossHealth.Damaged += HandleBossDamaged;
        _playerHealth.DamageReceived += HandlePlayerDamaged;
    }

    void Start()
    {
        _effects = Game.Instance.EffectManager;
        if (!_effects.IsReady) _effects.Initialize();
    }

    void OnDisable()
    {
        if (_bossHealth != null) _bossHealth.Damaged -= HandleBossDamaged;
        if (_playerHealth != null) _playerHealth.DamageReceived -= HandlePlayerDamaged;
    }

    /// <summary>플레이어가 보스에 가한 실제 피해의 접촉점에 타격 섬광을 재생한다.</summary>
    private void HandleBossDamaged(DamageContext context)
    {
        if (context.Instigator != _playerHealth.gameObject) return;
        _effects.Play(_bossHitEffect, context.Point, Quaternion.LookRotation(-_playerMotor.ViewForward));
    }

    /// <summary>플레이어의 실제 피해를 시야 앞 혈흔으로 표시한다. 방어 성공 이벤트는 처리하지 않는다.</summary>
    private void HandlePlayerDamaged(DamageContext context)
    {
        Vector3 forward = _playerMotor.ViewForward;
        Vector3 point = _playerMotor.ViewOrigin + forward * _playerEffectDistance
            + Vector3.up * _playerEffectVerticalOffset;
        _effects.Play(_playerHitEffect, point, Quaternion.LookRotation(-forward));
    }
}
