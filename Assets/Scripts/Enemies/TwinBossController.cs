using System;
using System.Collections;

using UnityEngine;

using UnityEngine.AI;

public enum TwinBossPattern { Idle, Cooperation, HumanRanged, BeastZigzag, BeastJump, Finished, Evading = 7 }

/// <summary>생존 조건별 협력·단독 패턴과 피격된 보스의 회피 및 종료 수명을 관리한다.</summary>
[DisallowMultipleComponent]
public sealed class TwinBossController : MonoBehaviour
{
    [Header("보스 연결")]
    [SerializeField] private HumanBossAttack _human;
    [SerializeField] private BeastBossAttack _beast;
    [SerializeField, Tooltip("비어 있으면 현재 Game.Player를 사용합니다.")]
    private KatanaDamageReceiver _player;
    private PlayerMotor _playerMotor;
    private EnemyMotor _humanMotor;
    private EnemyMotor _beastMotor;
    private NavMeshAgent _humanAgent;
    private NavMeshAgent _beastAgent;
    private Coroutine _battle;
    private Coroutine _patternRoutine;
    private bool _started;
    private bool _subscribed;
    private bool _patternFinished;
    private bool _interrupted;
    private bool _hitRequested;
    private EnemyHealth _hitBoss;
    private TwinBossPattern _pattern;

    [Header("패턴 선택 가중치")]
    [SerializeField, Min(0f)] private float _cooperationWeight = 2f;
    [SerializeField, Min(0f)] private float _humanRangedWeight = 1f;
    [SerializeField, Min(0f)] private float _beastZigzagWeight = 1f;
    [SerializeField, Min(0f)] private float _beastJumpWeight = 1f;

    [Header("패턴 시간")]
    [SerializeField, Min(0f)] private float _patternWaitMin = 1f;
    [SerializeField, Min(0f)] private float _patternWaitMax = 2f;
    [SerializeField, Min(0.1f)] private float _cooperationDuration = 10f;
    [SerializeField, Min(0f), Tooltip("회피 이동 후 다음 패턴까지의 휴식 시간(초)")]
    private float _breakRecovery = 2f;

    [Header("피격 회피 이동")]
    [SerializeField, Min(0f), Tooltip("실제 피격 후 추가 타격을 패링하는 무적 시간(초)")]
    private float _hitInvulnerabilityDuration = 2f;
    [SerializeField, Min(0f)] private float _deflectPushDistance = 3f;
    [SerializeField, Min(0.01f)] private float _deflectPushDuration = 0.16f;
    [SerializeField, Min(1f)] private float _evadeRadius = 6f;
    [SerializeField, Min(0.1f)] private float _evadeSpeed = 10f;
    [SerializeField, Min(0.5f)] private float _evadeMinimumDistance = 3f;
    [SerializeField, Min(0.1f), Tooltip("빠른 이동 가속도(미터/초 제곱)")]
    private float _moveAcceleration = 40f;
    [SerializeField, Min(0.1f), Tooltip("피격 회피 이동 최대 시간(초)")]
    private float _moveTimeout = 2f;
    [SerializeField] private LayerMask _movementObstacleMask = Physics.DefaultRaycastLayers;
    private readonly Collider[] _movementContacts = new Collider[16];
    private NavMeshPath _movePath;
    private float _humanSpeed;
    private float _humanAcceleration;
    private float _beastSpeed;
    private float _beastAcceleration;
    private float _humanInvulnerableUntil;
    private float _beastInvulnerableUntil;

    public TwinBossPattern Pattern => _pattern;
    public event Action<TwinBossPattern> PatternChanged;
    public event Action CooperationBroken;
    public event Action BattleFinished;

    void Start()
    {
        _started = true;
        BeginBattle();
    }

    void OnEnable()
    {
        if (_started) BeginBattle();
    }

    void Update()
    {
        if (!_subscribed) return;
        if (_humanInvulnerableUntil > 0f && Time.time >= _humanInvulnerableUntil)
        {
            _humanInvulnerableUntil = 0f;
            _human.Health.SetInvulnerable(false);
        }
        if (_beastInvulnerableUntil > 0f && Time.time >= _beastInvulnerableUntil)
        {
            _beastInvulnerableUntil = 0f;
            _beast.Health.SetInvulnerable(false);
        }
    }

    /// <summary>필수 연결과 독립 루트를 확인하고 이동 참조를 캐싱하며 실제 피해·사망 사건을 구독한다.</summary>
    private void BeginBattle()
    {
        if (_player == null && Game.Player != null)
            _player = Game.Player.GetComponent<KatanaDamageReceiver>();
        if (_human == null || _beast == null || _player == null || !_human.IsConfigured
            || _beast.RiderSeat == null || !_human.IsReady || !_beast.IsReady
            || _human.transform.IsChildOf(_beast.transform) || _beast.transform.IsChildOf(_human.transform)
            || transform == _human.transform || transform == _beast.transform)
        {
            Debug.LogError("쌍둥이 보스의 독립 루트·플레이어·사격·탑승·NavMesh 연결을 확인하세요.", this);
            enabled = false;
            return;
        }
        _humanMotor = _human.GetComponent<EnemyMotor>();
        _playerMotor = _player.GetComponent<PlayerMotor>();
        _beastMotor = _beast.GetComponent<EnemyMotor>();
        _humanAgent = _human.GetComponent<NavMeshAgent>();
        _beastAgent = _beast.GetComponent<NavMeshAgent>();
        _humanSpeed = _humanAgent.speed;
        _humanAcceleration = _humanAgent.acceleration;
        _beastSpeed = _beastAgent.speed;
        _beastAcceleration = _beastAgent.acceleration;
        _movePath = new NavMeshPath();
        _hitRequested = false;
        _hitBoss = null;
        _interrupted = false;
        _human.Health.Damaged += HandleHumanDamaged;
        _beast.Health.Damaged += HandleBeastDamaged;
        _human.Health.DamageBlocked += HandleHumanDamageBlocked;
        _beast.Health.DamageBlocked += HandleBeastDamageBlocked;
        _human.Health.Died += HandleBossDied;
        _beast.Health.Died += HandleBossDied;
        _subscribed = true;
        _battle = StartCoroutine(RunBattle());
    }

    /// <summary>생존 조건에 맞는 패턴을 실행하고 실제 피격은 회피·회복으로 전환하며 전투 종료를 정리한다.</summary>
    private IEnumerator RunBattle()
    {
        yield return null;
        while (CanBattle())
        {
            bool recovered = _hitRequested;
            if (recovered) yield return Evade();
            if (!CanBattle()) break;
            ChangePattern(TwinBossPattern.Idle);
            float until = recovered ? Time.time : Time.time + UnityEngine.Random.Range(_patternWaitMin,
                Mathf.Max(_patternWaitMin, _patternWaitMax));
            while (Time.time < until && !_hitRequested && CanBattle()) yield return null;
            if (!CanBattle()) break;
            if (_hitRequested) continue;
            _interrupted = false;
            _patternFinished = false;
            ChangePattern(SelectPattern());
            _patternRoutine = StartCoroutine(RunPattern());
            while (!_patternFinished && !_interrupted && !_hitRequested && CanBattle()) yield return null;
            CancelPattern();
        }
        CancelPattern();
        _battle = null;
        ChangePattern(TwinBossPattern.Finished);
        if (_human != null && _beast != null && !_human.Health.IsAlive && !_beast.Health.IsAlive)
            BattleFinished?.Invoke();
        Unsubscribe();
    }

    /// <summary>현재 선택한 협력 또는 단독 패턴을 실행하고 정상 종료를 기록한다.</summary>
    private IEnumerator RunPattern()
    {
        yield return null;
        switch (_pattern)
        {
            case TwinBossPattern.Cooperation: yield return Cooperate(); break;
            case TwinBossPattern.HumanRanged:
                _human.StartShooting(_player, false);
                while (_human.IsAttacking || _human.HasProjectiles) yield return null;
                break;
            case TwinBossPattern.BeastZigzag: yield return _beast.Zigzag(_player); break;
            case TwinBossPattern.BeastJump: yield return _beast.Jump(_player); break;
        }
        _patternFinished = true;
        _patternRoutine = null;
    }

    /// <summary>탑승 원형 이동과 사격을 병행하며 제한 시간이 끝나면 탑승 점프로 마무리한다.</summary>
    private IEnumerator Cooperate()
    {
        _human.Mount(_beast.RiderSeat);
        _beast.BeginOrbit(_player.transform);
        _human.StartShooting(_player, true);
        float until = Time.time + _cooperationDuration;
        while (Time.time < until && BothAlive() && !_hitRequested)
        {
            _beast.TickOrbit(_player.transform);
            yield return null;
        }
        _human.StopShooting();
        _beast.EndOrbit();
        if (BothAlive() && !_hitRequested) yield return _beast.Jump(_player);
        _human.Dismount();
    }

    /// <summary>최초 피격 보스만 빈 위치로 빠르게 이동시키고 회복 후 피격 대상과 요청을 해제한다.</summary>
    private IEnumerator Evade()
    {
        ChangePattern(TwinBossPattern.Evading);
        bool humanHit = _hitBoss == _human.Health;
        EnemyMotor motor = humanHit ? _humanMotor : _beastMotor;
        NavMeshAgent agent = humanHit ? _humanAgent : _beastAgent;
        Vector3 position = agent.transform.position;
        bool moving = _hitBoss != null && _hitBoss.IsAlive && motor.IsReady
            && TryEscape(agent, out position);
        if (moving) moving = BeginMove(motor, agent, position, _evadeSpeed);
        float until = Time.time + _moveTimeout;
        while (Time.time < until && CanBattle() && moving && _hitBoss != null
            && _hitBoss.IsAlive && !motor.HasArrived) yield return null;
        RestoreMovement();
        until = Time.time + _breakRecovery;
        while (Time.time < until && CanBattle()) yield return null;
        _hitRequested = false;
        _hitBoss = null;
        _interrupted = false;
    }

    /// <summary>agent가 플레이어 주변의 현재 위치와 떨어진 빈 지면으로 회피하도록 destination을 반환한다.</summary>
    private bool TryEscape(NavMeshAgent agent, out Vector3 destination)
    {
        destination = agent.transform.position;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 desired = _player.transform.position
                + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _evadeRadius;
            if (!TryDestination(agent, desired, out Vector3 candidate)
                || (candidate - agent.transform.position).sqrMagnitude < _evadeMinimumDistance * _evadeMinimumDistance) continue;
            destination = candidate;
            return true;
        }
        return false;
    }

    /// <summary>agent의 빈 NavMesh 지면과 완전한 경로를 확인해 desired의 destination을 반환한다.</summary>
    private bool TryDestination(NavMeshAgent agent, Vector3 desired, out Vector3 destination)
    {
        desired.y = agent.nextPosition.y;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        bool found = NavMesh.SamplePosition(desired, out NavMeshHit hit, 0.5f, filter);
        destination = hit.position;
        if (!found || !agent.CalculatePath(destination, _movePath)
            || _movePath.status != NavMeshPathStatus.PathComplete) return false;
        float radius = agent.radius;
        int count = Physics.OverlapCapsuleNonAlloc(destination + Vector3.up * (radius + 0.05f),
            destination + Vector3.up * Mathf.Max(radius + 0.05f, agent.height - radius), radius,
            _movementContacts, _movementObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == _movementContacts.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Transform contact = _movementContacts[i].transform;
            if (contact.IsChildOf(agent.transform)) continue;
            return false;
        }
        return true;
    }

    /// <summary>motor·agent의 속도와 가속도를 설정하고 destination으로 지상 이동을 시작해 수락 여부를 반환한다.</summary>
    private bool BeginMove(EnemyMotor motor, NavMeshAgent agent, Vector3 destination, float speed)
    {
        agent.speed = speed;
        agent.acceleration = _moveAcceleration;
        return motor.MoveTo(destination, 0.1f);
    }

    /// <summary>두 보스의 빠른 이동을 정지하고 초기 속도·가속도를 복원한다.</summary>
    private void RestoreMovement()
    {
        if (_humanAgent != null)
        {
            _humanMotor.Stop();
            _humanAgent.speed = _humanSpeed;
            _humanAgent.acceleration = _humanAcceleration;
        }
        if (_beastAgent != null)
        {
            _beastMotor.Stop();
            _beastAgent.speed = _beastSpeed;
            _beastAgent.acceleration = _beastAcceleration;
        }
    }

    /// <summary>생존한 보스의 개별 패턴과 둘 다 살아 있을 때의 탑승 패턴을 가중치로 선택해 반환한다.</summary>
    private TwinBossPattern SelectPattern()
    {
        bool humanAlive = _human.Health.IsAlive;
        bool beastAlive = _beast.Health.IsAlive;
        float cooperation = humanAlive && beastAlive ? _cooperationWeight : 0f;
        float human = humanAlive ? _humanRangedWeight : 0f;
        float zigzag = beastAlive ? _beastZigzagWeight : 0f;
        float jump = beastAlive ? _beastJumpWeight : 0f;
        float total = cooperation + human + zigzag + jump;
        if (total <= 0f) return humanAlive ? TwinBossPattern.HumanRanged : TwinBossPattern.BeastZigzag;
        float choice = UnityEngine.Random.value * total;
        if (choice < cooperation) return TwinBossPattern.Cooperation;
        choice -= cooperation;
        if (choice < human) return TwinBossPattern.HumanRanged;
        choice -= human;
        if (choice < zigzag) return TwinBossPattern.BeastZigzag;
        return jump > 0f ? TwinBossPattern.BeastJump
            : (zigzag > 0f ? TwinBossPattern.BeastZigzag
                : (human > 0f ? TwinBossPattern.HumanRanged : TwinBossPattern.Cooperation));
    }

    /// <summary>플레이어와 하나 이상의 보스가 생존했는지 반환한다.</summary>
    private bool CanBattle()
    {
        return _player != null && _player.IsAlive && _human != null && _beast != null
            && (_human.Health.IsAlive || _beast.Health.IsAlive);
    }

    /// <summary>협력할 두 보스가 모두 생존했는지 반환한다.</summary>
    private bool BothAlive()
    {
        return _human != null && _beast != null && _human.Health.IsAlive && _beast.Health.IsAlive;
    }

    /// <summary>인간의 실제 피해 사건을 전달해 인간을 회피 대상으로 기록한다.</summary>
    private void HandleHumanDamaged(DamageContext context)
    {
        HandleBossDamaged(_human.Health);
    }

    /// <summary>짐승의 실제 피해 사건을 전달해 짐승을 회피 대상으로 기록한다.</summary>
    private void HandleBeastDamaged(DamageContext context)
    {
        HandleBossDamaged(_beast.Health);
    }

    /// <summary>health를 최초 피격 회피 대상으로 기록하며 회피·회복 중 추가 피해는 대상을 바꾸지 않는다.</summary>
    private void HandleBossDamaged(EnemyHealth health)
    {
        if (health.IsAlive && _hitInvulnerabilityDuration > 0f)
        {
            health.SetInvulnerable(true);
            if (health == _human.Health)
                _humanInvulnerableUntil = Time.time + _hitInvulnerabilityDuration;
            else
                _beastInvulnerableUntil = Time.time + _hitInvulnerabilityDuration;
        }
        if (_hitRequested || _pattern == TwinBossPattern.Evading || _pattern == TwinBossPattern.Finished) return;
        _hitRequested = true;
        _hitBoss = health;
        if (_pattern == TwinBossPattern.Cooperation)
            CooperationBroken?.Invoke();
        CancelPattern();
    }

    /// <summary>
    /// 인간에게 차단된 context를 전달해 인간의 무적을 해제하고 플레이어 타격을 쳐낸다.
    /// </summary>
    private void HandleHumanDamageBlocked(DamageContext context)
    {
        HandleBossDamageBlocked(_human.Health, context);
    }

    /// <summary>짐승에게 차단된 context를 전달해 짐승의 무적을 해제하고 플레이어 타격을 쳐낸다.</summary>
    private void HandleBeastDamageBlocked(DamageContext context)
    {
        HandleBossDamageBlocked(_beast.Health, context);
    }

    /// <summary>
    /// health가 무적으로 차단한 플레이어 공격 context에 패링 효과·소리·검 튕김을 적용한다.
    /// 플레이어를 시점 뒤로 설정 거리만큼 밀고 해당 보스의 무적과 종료 시각을 즉시 해제한다.
    /// </summary>
    private void HandleBossDamageBlocked(EnemyHealth health, DamageContext context)
    {
        if (context.Instigator != _player.gameObject) return;
        health.SetInvulnerable(false);
        if (health == _human.Health) _humanInvulnerableUntil = 0f;
        else _beastInvulnerableUntil = 0f;
        Vector3 direction = -_playerMotor.ViewForward;
        _playerMotor.ApplyCombatPush(direction, _deflectPushDistance, _deflectPushDuration);
        KatanaWeaponVisual weapon = _player.GetComponent<KatanaWeaponVisual>();
        if (weapon != null) weapon.PlayAttackDeflection();
        Game.Instance.EffectManager.Play(EffectId.ParryClash, context.Point, Quaternion.LookRotation(direction));
        Managers.Sound.Play(SoundId.ParryClash, context.Point, 1f, gameObject);
    }

    /// <summary>health 사망을 기록해 남은 연계를 중단하며 회피 중에는 생존자 이동을 유지한다.</summary>
    private void HandleBossDied(EnemyHealth health)
    {
        _interrupted = true;
        if (_pattern != TwinBossPattern.Evading) CancelPattern();
    }

    /// <summary>패턴 코루틴·탄환·탑승·점프를 취소하고 빠른 이동 설정을 복원한다.</summary>
    private void CancelPattern()
    {
        if (_patternRoutine != null) StopCoroutine(_patternRoutine);
        _patternRoutine = null;
        if (_human != null)
        {
            _human.StopShooting();
            _human.Dismount();
        }
        if (_beast != null) _beast.CancelAttack();
        RestoreMovement();
    }

    /// <summary>pattern 변경을 저장하고 표현 담당자에게 전달한다.</summary>
    private void ChangePattern(TwinBossPattern pattern)
    {
        if (_pattern == pattern) return;
        _pattern = pattern;
        PatternChanged?.Invoke(pattern);
    }

    /// <summary>실제 피해·사망 사건의 전투 구독을 해제한다.</summary>
    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        if (_human != null)
        {
            _human.Health.Damaged -= HandleHumanDamaged;
            _human.Health.DamageBlocked -= HandleHumanDamageBlocked;
            _human.Health.SetInvulnerable(false);
            _human.Health.Died -= HandleBossDied;
        }
        if (_beast != null)
        {
            _beast.Health.Damaged -= HandleBeastDamaged;
            _beast.Health.DamageBlocked -= HandleBeastDamageBlocked;
            _beast.Health.SetInvulnerable(false);
            _beast.Health.Died -= HandleBossDied;
        }
        _humanInvulnerableUntil = 0f;
        _beastInvulnerableUntil = 0f;
    }

    void OnDisable()
    {
        if (_battle != null) StopCoroutine(_battle);
        _battle = null;
        CancelPattern();
        Unsubscribe();
        _hitRequested = false;
        _hitBoss = null;
        ChangePattern(TwinBossPattern.Idle);
    }
}
