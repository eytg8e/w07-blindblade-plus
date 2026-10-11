using System;
using System.Collections;

using UnityEngine;

using UnityEngine.AI;

public enum BossState { Idle, Roaming, Charging, ChainCharge, Duel, Stunned, Dead }

/// <summary>보스 행동의 단일 실행과 플레이어 피해·방어 결과 및 종료 수명을 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMotor), typeof(EnemySensor))]
public sealed class BossController : MonoBehaviour
{
    [Header("전투 연결")]
    [SerializeField] private BossMeleeAttack _melee;
    private EnemyHealth _health;
    private EnemyMotor _motor;
    private EnemySensor _sensor;
    private NavMeshAgent _agent;
    private PlayerController _player;
    private PlayerHealth _playerHealth;
    private KatanaCombatCoordinator _combat;
    private Coroutine _routine;
    private int _strike;
    private int _parriedStrikes;
    private bool _strikeResolved;
    private bool _playerHit;
    private BossState _state;
    private bool _started;

    public BossState State => _state;
    public event Action<BossState> StateChanged;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _motor = GetComponent<EnemyMotor>();
        _sensor = GetComponent<EnemySensor>();
        _agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        _started = true;
        InitializeBattle();
    }

    void OnEnable()
    {
        if (_started) InitializeBattle();
    }

    void Update()
    {
        if (_routine != null && (_player == null || !_playerHealth.IsAlive || !_motor.IsReady))
            StopBattle();
    }

    void OnDisable()
    {
        StopBattle();
    }

    /// <summary>현재 세션 플레이어와 필수 참조를 확인하고 전투 결과를 구독하여 행동을 시작한다.</summary>
    private void InitializeBattle()
    {
        _player = Game.Player;
        if (_player != null)
        {
            _playerHealth = _player.GetComponent<PlayerHealth>();
            _combat = _player.GetComponent<KatanaCombatCoordinator>();
        }
        if (_melee == null || _player == null || _playerHealth == null || _combat == null || !_motor.IsReady)
        {
            Debug.LogError("보스의 근접 공격·플레이어·방어 계약·NavMesh 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }
        _sensor.SetTarget(_player.transform);
        _health.Damaged += HandleBossDamaged;
        _health.Died += HandleBossDied;
        _playerHealth.DamageReceived += HandlePlayerDamaged;
        _combat.DefenseSucceeded += HandleDefense;
        _routine = StartCoroutine(RunBattle());
    }

    /// <summary>생존 중 단일 행동 흐름을 유지하고 전투 종료 시 코루틴 참조를 정리한다.</summary>
    private IEnumerator RunBattle()
    {
        while (_health.IsAlive && _playerHealth.IsAlive)
            yield return null;
        _routine = null;
    }

    /// <summary>context의 보스 피격을 받는다. 기본 피해 적용에 추가 스턴을 부여하지 않는다.</summary>
    private void HandleBossDamaged(DamageContext context)
    {
    }

    /// <summary>context가 현재 보스 3연격의 피해이면 남은 공격을 중단하도록 적중을 기록한다.</summary>
    private void HandlePlayerDamaged(DamageContext context)
    {
        if (_state == BossState.Duel && context.Instigator == gameObject && context.PatternId == _melee.PatternId)
        {
            _playerHit = true;
            _strikeResolved = true;
            _melee.CancelAttack();
        }
    }

    /// <summary>result의 공격자와 패턴을 확인해 현재 타격의 방어를 한 번만 기록하고 정확한 패링만 집계한다.</summary>
    private void HandleDefense(KatanaDefenseResult result)
    {
        if (_state != BossState.Duel || _strike <= 0 || _strikeResolved
            || result.Incoming.Instigator != gameObject || result.Incoming.PatternId != _melee.PatternId)
            return;
        _strikeResolved = true;
        if (result.IsParry) _parriedStrikes++;
    }

    /// <summary>사망한 health의 행동을 종료하고 사망 상태를 구독자에게 전달한다.</summary>
    private void HandleBossDied(EnemyHealth health)
    {
        StopBattle();
        ChangeState(BossState.Dead);
    }

    /// <summary>state로 행동 상태를 변경하고 실제 변경이 있을 때 표현 담당자에게 알린다.</summary>
    private void ChangeState(BossState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(state);
    }

    /// <summary>실행 중인 공격·이동·코루틴을 중지하고 전투 결과 구독과 타격 상태를 정리한다.</summary>
    private void StopBattle()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (_melee != null) _melee.CancelAttack();
        _motor.Stop();
        _health.Damaged -= HandleBossDamaged;
        _health.Died -= HandleBossDied;
        if (_playerHealth != null) _playerHealth.DamageReceived -= HandlePlayerDamaged;
        if (_combat != null) _combat.DefenseSucceeded -= HandleDefense;
        _strike = 0;
        _strikeResolved = false;
        if (_state != BossState.Dead) ChangeState(BossState.Idle);
    }
}
