using UnityEngine;


/// <summary>
/// 실제 적 피해·사망·추적 상태 사건을 중앙 사운드 설정에 연결한다.
/// 사망 판정은 적 시스템에 맡기고 사망음은 적 루트 비활성화 후에도 자연 종료한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyDamageAudio : MonoBehaviour
{
    [Header("Damage Sound")]
    [SerializeField] private SoundId _damageSound = SoundId.SwordHit;
    [SerializeField, Min(0f)] private float _volume = 0.8f;
    private EnemyHealth _health;

    [Header("적 상태 연결")]
    private EnemyController _controller;
    private EnemySensor _sensor;
    private Vector3 _lastDamagePoint;
    private bool _hasDamagePoint;
    private bool _deathPlayed;
    private bool _discoveryPlayed;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _controller = GetComponent<EnemyController>();
        _sensor = GetComponent<EnemySensor>();
    }

    void OnEnable()
    {
        _health.Damaged += HandleDamage;
        _health.Died += HandleDeath;
        _deathPlayed = false;
        _discoveryPlayed = false;
        _hasDamagePoint = false;
        if (_controller != null)
        {
            _controller.StateChanged += HandleState;
            HandleState(_controller.State);
        }
    }

    void OnDisable()
    {
        _health.Damaged -= HandleDamage;
        _health.Died -= HandleDeath;
        if (_controller != null) _controller.StateChanged -= HandleState;
    }

    /// <summary>
    /// context의 피해 지점과 적용 후 체력으로 일반 피격 또는 치명타격을 한 번 재생한다.
    /// 사망 확정 음원은 Died 사건에서 처리하고 치명타에는 일반 피격음을 중복 재생하지 않는다.
    /// </summary>
    private void HandleDamage(DamageContext context)
    {
        _lastDamagePoint = context.Point;
        _hasDamagePoint = true;
        if (_health.CurrentHealth <= 0f)
            Managers.Sound.PlayCue(CombatSoundCue.EnemyFinished, context.Point, context.Instigator);
        else
            Managers.Sound.Play(_damageSound, context.Point, _volume, context.Instigator);
    }

    /// <summary>
    /// health의 사망 확정에 마지막 피해 지점 또는 적 위치에서 사망음을 한 번 재생한다.
    /// 소유자를 지정하지 않아 이어지는 적 루트 비활성화에도 사망음의 끝을 유지한다.
    /// </summary>
    private void HandleDeath(EnemyHealth health)
    {
        if (_deathPlayed) return;
        _deathPlayed = true;
        Managers.Sound.PlayCue(CombatSoundCue.EnemyDie, _hasDamagePoint ? _lastDamagePoint : health.transform.position);
    }

    /// <summary>
    /// state가 실제 시야 확보 후 추적·공격으로 전환되면 발견 전조음을 한 번 재생한다.
    /// 수색·대기·순찰로 돌아가면 재발견을 허용하며 반복 공격 전환에서는 중복 재생하지 않는다.
    /// </summary>
    private void HandleState(EnemyState state)
    {
        if (state == EnemyState.Idle || state == EnemyState.Patrolling || state == EnemyState.Searching)
        {
            _discoveryPlayed = false;
            return;
        }
        if (_discoveryPlayed || !_health.IsAlive || !_sensor.CanSeeTarget()) return;
        _discoveryPlayed = true;
        Managers.Sound.PlayCue(CombatSoundCue.EnemyPrepare, transform.position, gameObject, true);
    }
}
