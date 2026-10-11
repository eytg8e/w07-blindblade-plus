using System;
using System.Collections;

using UnityEngine;

using UnityEngine.AI;

public enum BossState { Idle, Roaming, Charging, ChainCharge, Duel, Stunned, Dead, Disappeared }

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
    private PlayerMotor _playerMotor;
    private PlayerHealth _playerHealth;
    private KatanaCombatCoordinator _combat;
    private Coroutine _routine;
    private int _strike;
    private int _parriedStrikes;
    private bool _strikeResolved;
    private bool _playerHit;
    private BossState _state;
    private bool _started;
    [SerializeField, Min(0f), Tooltip("피격으로 행동을 취소한 뒤 사라지는 시간(초)")]
    private float _hitRecoveryDuration = 0.5f;
    [SerializeField, Tooltip("피격 후 잠시 숨을 전투 공간 밖의 월드 위치")]
    private Vector3 _disappearPosition = new Vector3(0f, -100f, 0f);
    private Collider[] _bodyColliders;
    private bool[] _colliderEnabled;
    private Vector3 _returnPosition;
    private bool _hidden;
    private bool _hiddenUpdatePosition;
    private BossState _resumeState;

    [Header("배회")]
    [SerializeField, Min(0.1f), Tooltip("마지막 피격 이후 돌진까지의 시간(초)")]
    private float _roamDuration = 7f;
    [SerializeField, Min(1f), Tooltip("플레이어 주변 배회 반경(미터)")]
    private float _roamRadius = 7f;
    [SerializeField, Min(0.1f)] private float _roamDestinationInterval = 2.5f;
    [SerializeField, Min(0.1f)] private float _roamSpeed = 2f;
    private float _roamUntil;
    private NavMeshPath _path;

    [Header("돌진")]
    [SerializeField, Min(0.1f), Tooltip("기합 이후 돌진 준비 시간(초)")]
    private float _chargePreparation = 0.9f;
    [SerializeField, Min(0.1f)] private float _chargeSpeed = 14f;
    [SerializeField, Min(0.1f)] private float _chargeDistance = 12f;
    [SerializeField, Min(0.05f)] private float _chargeDuration = 1f;
    [SerializeField, Min(0f)] private float _chargeDamage = 15f;
    [SerializeField, Min(0.1f)] private float _chargeHitRadius = 0.65f;
    [SerializeField] private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;
    private readonly RaycastHit[] _obstacleHits = new RaycastHit[16];
    private readonly Collider[] _chargeContacts = new Collider[16];

    public event Action ChargePreparing;
    public event Action Teleported;

    [Header("연속 돌진")]
    [SerializeField, Min(1f)] private float _teleportRadius = 6f;
    [SerializeField, Range(1f, 180f), Tooltip("플레이어 시점 기준 돌진 등장 각도(도). 180도는 전방 반원입니다.")]
    private float _teleportArc = 180f;
    [SerializeField, Min(0.1f)] private float _chargeInterval = 0.7f;
    private readonly Collider[] _placementContacts = new Collider[16];

    [Header("정면승부")]
    [SerializeField, Min(0.8f), Tooltip("플레이어 정면 배치 거리(미터)")]
    private float _duelDistance = 1.6f;
    [SerializeField, Min(0.1f)] private float _duelPreparation = 0.6f;
    [SerializeField, Min(0.1f)] private float _strikeInterval = 0.45f;
    [SerializeField, Min(0.1f)] private float _stunDuration = 3f;
    private bool _duelPoseActive;
    private bool _previousUpdatePosition;
    private bool _previousUpdateRotation;
    private Vector3 _duelNavigationPosition;

    public BossState State => _state;
    public event Action<BossState> StateChanged;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _motor = GetComponent<EnemyMotor>();
        _sensor = GetComponent<EnemySensor>();
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
        _bodyColliders = GetComponentsInChildren<Collider>(true);
        _colliderEnabled = new bool[_bodyColliders.Length];
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
            _playerMotor = _player.GetComponent<PlayerMotor>();
            _combat = _player.GetComponent<KatanaCombatCoordinator>();
        }
        if (_melee == null || _player == null || _playerMotor == null || _playerHealth == null || _combat == null || !_motor.IsReady)
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
    private IEnumerator RunBattle(BossState startingState = BossState.Roaming)
    {
        yield return null;
        while (_health.IsAlive && _playerHealth.IsAlive)
        {
            if (startingState == BossState.Roaming) yield return Roam();
            if ((startingState == BossState.Roaming || startingState == BossState.Charging) && TryTeleportAround())
            {
                ChangeState(BossState.Charging);
                yield return Charge();
            }
            if (startingState != BossState.Duel) yield return ChainCharge();
            yield return Duel();
            startingState = BossState.Roaming;
        }
        _routine = null;
    }

    /// <summary>마지막 피격 기준 제한 시간 동안 플레이어 주변의 유효한 경로를 골라 배회한다.</summary>
    private IEnumerator Roam()
    {
        ChangeState(BossState.Roaming);
        _roamUntil = Time.time + _roamDuration;
        _agent.speed = _roamSpeed;
        float nextDestination = 0f;
        while (Time.time < _roamUntil)
        {
            if (Time.time >= nextDestination)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * _roamRadius;
                if (TrySamplePosition(_player.transform.position + new Vector3(offset.x, 0f, offset.y), out Vector3 position)
                    && _agent.CalculatePath(position, _path) && _path.status == NavMeshPathStatus.PathComplete)
                    _motor.MoveTo(position, 0.1f);
                nextDestination = Time.time + _roamDestinationInterval;
            }
            yield return null;
        }
        _motor.Stop();
    }

    /// <summary>기합과 준비 시간 뒤 목표 방향을 고정해 장애물 경계까지 돌진하고 접촉 피해를 한 번 적용한다.</summary>
    private IEnumerator Charge()
    {
        _motor.Stop();
        ChargePreparing?.Invoke();
        yield return new WaitForSeconds(_chargePreparation);
        Vector3 direction = Vector3.ProjectOnPlane(_player.transform.position - transform.position, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.001f) yield break;
        transform.rotation = Quaternion.LookRotation(direction);
        float remaining = _chargeDistance;
        float until = Time.time + _chargeDuration;
        bool hitPlayer = false;
        while (Time.time < until && remaining > 0.01f)
        {
            Vector3 previous = transform.position;
            float distance = Mathf.Min(_chargeSpeed * Time.deltaTime, remaining);
            Vector3 center = previous + Vector3.up;
            int count = Physics.SphereCastNonAlloc(center, _agent.radius, direction, _obstacleHits,
                distance, _obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform obstacle = _obstacleHits[i].transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(_player.transform)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, _obstacleHits[i].distance - 0.05f));
            }
            Vector3 destination = previous + direction * distance;
            if (_agent.Raycast(destination, out NavMeshHit boundary))
                destination = Vector3.MoveTowards(boundary.position, previous, 0.05f);
            _agent.Move(destination - previous);
            remaining -= Vector3.Distance(previous, transform.position);
            if (!hitPlayer)
            {
                count = Physics.OverlapCapsuleNonAlloc(center, transform.position + Vector3.up,
                    _chargeHitRadius, _chargeContacts, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (!_chargeContacts[i].transform.IsChildOf(_player.transform)) continue;
                    hitPlayer = true;
                    _playerHealth.TakeDamage(new DamageContext(_chargeDamage, gameObject,
                        _chargeContacts[i].ClosestPoint(center), direction, "BossCharge"));
                    break;
                }
            }
            if (distance < 0.01f || (transform.position - previous).sqrMagnitude < 0.000001f) break;
            yield return null;
        }
        _motor.Stop();
    }

    /// <summary>desired 주변의 같은 에이전트 유형·영역에 속한 지면을 찾아 position으로 반환한다.</summary>
    private bool TrySamplePosition(Vector3 desired, out Vector3 position)
    {
        desired.y = _agent.nextPosition.y;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        bool found = NavMesh.SamplePosition(desired, out NavMeshHit hit, 0.5f, filter);
        position = hit.position;
        return found;
    }

    /// <summary>주변 유효 위치로 순간이동하고 각 위치에서 준비·돌진을 총 세 번 반복한다.</summary>
    private IEnumerator ChainCharge()
    {
        for (int i = 0; i < 3; i++)
        {
            if (!TryTeleportAround()) yield break;
            ChangeState(BossState.ChainCharge);
            yield return Charge();
            yield return new WaitForSeconds(_chargeInterval);
        }
    }

    /// <summary>플레이어 전방 부채꼴의 랜덤 후보 중 유효한 지면과 빈 공간으로 순간이동하며 실패하면 false를 반환한다.</summary>
    private bool TryTeleportAround()
    {
        Vector3 forward = _playerMotor.ViewForward;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = UnityEngine.Random.Range(-_teleportArc * 0.5f, _teleportArc * 0.5f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * forward * _teleportRadius;
            if (!TrySamplePosition(_player.transform.position + offset, out Vector3 position)) continue;
            if (Vector3.Dot(position - _player.transform.position, forward) < 0f) continue;
            if (!TryPlace(position)) continue;
            return true;
        }
        _motor.Stop();
        return false;
    }

    /// <summary>position의 공간이 비어 있을 때 에이전트를 이동시키고 걸음 초기화를 알린다.</summary>
    private bool TryPlace(Vector3 position)
    {
        float radius = _agent.radius;
        int count = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (radius + 0.05f),
            position + Vector3.up * (_agent.height - radius), radius, _placementContacts,
            _obstacleMask, QueryTriggerInteraction.Ignore);
        if (count == _placementContacts.Length) return false;
        for (int i = 0; i < count; i++)
            if (!_placementContacts[i].transform.IsChildOf(transform)) return false;
        _motor.Stop();
        if (!_agent.Warp(position)) return false;
        Teleported?.Invoke();
        return true;
    }

    /// <summary>정면 준비 뒤 칼날 공격을 세 번 실행하고 유효타는 중단하며 세 번의 패링만 스턴으로 보상한다.</summary>
    private IEnumerator Duel()
    {
        _motor.Stop();
        _duelNavigationPosition = _agent.nextPosition;
        _previousUpdatePosition = _agent.updatePosition;
        _previousUpdateRotation = _agent.updateRotation;
        _duelPoseActive = true;
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        try
        {
            PlaceInFront();
            ChangeState(BossState.Duel);
            _parriedStrikes = 0;
            _playerHit = false;
            for (_strike = 1; _strike <= 3; _strike++)
            {
                _strikeResolved = false;
                float until = Time.time + _duelPreparation;
                while (Time.time < until)
                {
                    PlaceInFront();
                    yield return null;
                }
                if (!_melee.TryAttack(_player.transform)) yield break;
                while (_melee.IsBusy && !_playerHit) yield return null;
                if (_playerHit) break;
                yield return new WaitForSeconds(_strikeInterval);
            }
            _strike = 0;
            if (_parriedStrikes != 3 || _playerHit) yield break;
            _motor.Stop();
            ChangeState(BossState.Stunned);
            yield return new WaitForSeconds(_stunDuration);
        }
        finally
        {
            RestoreNavigation();
        }
    }

    /// <summary>플레이어 위치와 수평 시점으로 정면에 배치하며 준비 중에는 NavMesh 경계를 적용하지 않는다.</summary>
    private void PlaceInFront()
    {
        Vector3 forward = _playerMotor.ViewForward;
        transform.SetPositionAndRotation(_player.transform.position + forward * _duelDistance,
            Quaternion.LookRotation(-forward));
    }

    /// <summary>정면승부 종료·취소 시 유효한 지면으로 복귀하고 기존 에이전트 위치·회전 갱신 설정을 복원한다.</summary>
    private void RestoreNavigation()
    {
        if (!_duelPoseActive) return;
        _duelPoseActive = false;
        Vector3 position = transform.position;
        position.y = _duelNavigationPosition.y;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, _duelDistance + 1f, filter))
            position = hit.position;
        else
            position = _duelNavigationPosition;
        _agent.updatePosition = _previousUpdatePosition;
        _agent.updateRotation = _previousUpdateRotation;
        if (_agent.isActiveAndEnabled) _agent.Warp(position);
        Teleported?.Invoke();
    }

    /// <summary>context의 실제 피격 시 현재 행동을 취소하고 공격할 수 없는 위치로 잠시 이동한다.</summary>
    private void HandleBossDamaged(DamageContext context)
    {
        if (!_health.IsAlive) return;
        _resumeState = _state == BossState.Charging || _state == BossState.ChainCharge || _state == BossState.Duel
            ? _state : BossState.Roaming;
        if (_state == BossState.Roaming) _resumeState = BossState.Charging;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        RestoreNavigation();
        _melee.CancelAttack();
        _motor.Stop();
        _strike = 0;
        _strikeResolved = false;
        _parriedStrikes = 0;
        ChangeState(BossState.Disappeared);
        _returnPosition = transform.position;
        _hiddenUpdatePosition = _agent.updatePosition;
        _agent.updatePosition = false;
        _hidden = true;
        _health.SetInvulnerable(true);
        for (int i = 0; i < _bodyColliders.Length; i++)
        {
            _colliderEnabled[i] = _bodyColliders[i].enabled;
            _bodyColliders[i].enabled = false;
        }
        transform.position = _disappearPosition;
        _routine = StartCoroutine(RecoverFromHit());
    }

    /// <summary>피격 후 숨는 시간이 지나면 위치·피해 수신을 복구하고 중단했던 패턴부터 재개한다.</summary>
    private IEnumerator RecoverFromHit()
    {
        yield return new WaitForSeconds(_hitRecoveryDuration);
        RestoreAfterDisappearance();
        if (_health.IsAlive && _playerHealth.IsAlive) yield return RunBattle(_resumeState);
        _routine = null;
    }

    /// <summary>숨기 전 위치·에이전트·충돌체와 피해 수신을 복구한다. 종료 시에도 숨김 상태를 남기지 않는다.</summary>
    private void RestoreAfterDisappearance()
    {
        if (!_hidden) return;
        _hidden = false;
        transform.position = _returnPosition;
        _agent.updatePosition = _hiddenUpdatePosition;
        if (_agent.isActiveAndEnabled) _agent.Warp(_returnPosition);
        for (int i = 0; i < _bodyColliders.Length; i++) _bodyColliders[i].enabled = _colliderEnabled[i];
        _health.SetInvulnerable(false);
        Teleported?.Invoke();
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
        RestoreAfterDisappearance();
        RestoreNavigation();
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
