using UnityEngine;

/// <summary>두 보스의 타격 섬광과 플레이어 피격 혈흔을 기존 효과 풀로 재생한다.</summary>
[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class TwinBossHitEffects : MonoBehaviour
{
    [Header("피해 수신자")]
    [SerializeField] private EnemyHealth _humanHealth;
    [SerializeField] private EnemyHealth _beastHealth;
    [SerializeField, Tooltip("비어 있으면 현재 Game.Player의 체력을 연결합니다.")]
    private PlayerHealth _playerHealth;
    private PlayerMotor _playerMotor;
    private EffectManager _effects;
    private bool _started;
    private bool _subscribed;

    [Header("보스 적중")]
    [SerializeField] private EffectId _bossHitEffect = EffectId.BossDamageHit;

    [Header("플레이어 피격")]
    [SerializeField] private EffectId _playerHitEffect = EffectId.PlayerDamageHit;
    [SerializeField, Min(0.1f), Tooltip("카메라 앞 혈흔 표시 거리(미터)")]
    private float _playerEffectDistance = 0.8f;
    [SerializeField, Tooltip("시야 중심에서 혈흔의 수직 이동량(미터)")]
    private float _playerEffectVerticalOffset = -0.1f;

    void Start()
    {
        if (_playerHealth == null && Game.Player != null)
            _playerHealth = Game.Player.GetComponent<PlayerHealth>();
        if (_humanHealth == null || _beastHealth == null || _playerHealth == null)
        {
            Debug.LogError("쌍둥이 피격 효과의 인간·짐승·플레이어 체력 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }
        _playerMotor = _playerHealth.GetComponent<PlayerMotor>();
        _effects = Game.Instance.EffectManager;
        if (!_effects.IsReady && !_effects.Initialize())
        {
            Debug.LogError("쌍둥이 피격 효과에 필요한 공용 효과 풀을 초기화하지 못했습니다.", this);
            enabled = false;
            return;
        }
        _playerHealth.GetComponent<PlayerAttackSlashVfx>().SetUseOutlineShader(true);
        _started = true;
        Subscribe();
    }

    void OnEnable()
    {
        if (_started) Subscribe();
    }

    /// <summary>두 보스의 실제 피해와 플레이어의 실제 피해를 각각 한 번 구독한다.</summary>
    private void Subscribe()
    {
        if (_subscribed) return;
        _humanHealth.Damaged += HandleBossDamaged;
        _beastHealth.Damaged += HandleBossDamaged;
        _playerHealth.DamageReceived += HandlePlayerDamaged;
        _subscribed = true;
    }

    /// <summary>context가 플레이어 공격이나 반사탄 피해이면 접촉점에 기존 보스 타격 섬광을 재생한다.</summary>
    private void HandleBossDamaged(DamageContext context)
    {
        if (context.Instigator != _playerHealth.gameObject) return;
        _effects.Play(_bossHitEffect, context.Point, Quaternion.LookRotation(-_playerMotor.ViewForward));
    }

    /// <summary>context의 실제 플레이어 피해에서 시야 앞 지정 거리와 높이에 혈흔을 한 번 재생한다.</summary>
    private void HandlePlayerDamaged(DamageContext context)
    {
        Vector3 forward = _playerMotor.ViewForward;
        Vector3 point = _playerMotor.ViewOrigin + forward * _playerEffectDistance
            + Vector3.up * _playerEffectVerticalOffset;
        _effects.Play(_playerHitEffect, point, Quaternion.LookRotation(-forward));
    }

    void OnDisable()
    {
        if (!_subscribed) return;
        if (_humanHealth != null) _humanHealth.Damaged -= HandleBossDamaged;
        if (_beastHealth != null) _beastHealth.Damaged -= HandleBossDamaged;
        if (_playerHealth != null) _playerHealth.DamageReceived -= HandlePlayerDamaged;
        _subscribed = false;
    }
}
