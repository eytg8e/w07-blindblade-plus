using System;

using UnityEngine;

public enum EnemyState
{
    Idle,
    Chasing,
    Searching,
    Attacking,
    Patrolling
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySensor), typeof(EnemyMotor), typeof(EnemyAttackSelector))]
public class EnemyController : MonoBehaviour
{
    [Header("추적 설정")]
    [SerializeField, Min(0.02f)] private float _perceptionInterval = 0.1f;
    [SerializeField, Min(0.02f)] private float _repathInterval = 0.2f;
    [SerializeField, Min(0f)] private float _memoryDuration = 3f;
    [SerializeField, Tooltip("끄면 공격 범위 밖이나 돌진 경로가 막힌 경우 걸어서 접근하지 않고 대기합니다.")]
    private bool _allowWalkingChase = true;
    private EnemySensor _sensor;
    private EnemyMotor _motor;
    private EnemyAttackSelector _attackSelector;
    private EnemyPatrol _patrol;
    private bool _hasVisibleTarget;
    private EnemyState _state;
    private Vector3 _lastKnownPosition;
    private float _lastSeenTime;
    private float _nextPerceptionTime;
    private float _nextRepathTime;
    private Vector3 _heardPosition;
    private float _noiseAlertUntil = float.NegativeInfinity;

    public EnemyState State => _state;
    private bool AllowsWalkingChase => _allowWalkingChase || _attackSelector.MeleeOnly;
    public event Action<EnemyState> StateChanged;

    protected EnemySensor Sensor => _sensor;
    protected EnemyMotor Motor => _motor;
    protected EnemyAttackBehaviour Attack => _attackSelector.CurrentAttack;
    protected EnemyAttackSelector AttackSelector => _attackSelector;

    protected virtual void Awake()
    {
        _sensor = GetComponent<EnemySensor>();
        _motor = GetComponent<EnemyMotor>();
        _attackSelector = GetComponent<EnemyAttackSelector>();
        _patrol = GetComponent<EnemyPatrol>();
    }

    protected virtual void OnEnable()
    {
        _nextPerceptionTime = 0f;
        _nextRepathTime = 0f;
        _lastSeenTime = float.NegativeInfinity;
        _lastKnownPosition = transform.position;
        _hasVisibleTarget = false;
        _noiseAlertUntil = float.NegativeInfinity;
    }

    protected virtual void Update()
    {
        if (!_motor.IsReady)
        {
            _attackSelector.CancelCurrentAttack();
            ChangeState(EnemyState.Idle);
            return;
        }

        if (_attackSelector.IsStaggered)
        {
            _motor.Stop();
            ChangeState(EnemyState.Attacking);
            return;
        }
        if (_attackSelector.IsBusy)
        {
            ChangeState(EnemyState.Attacking);
            if (!_attackSelector.CurrentAttack.AllowsMovement)
                _motor.Stop();
            return;
        }

        if (Time.time >= _nextPerceptionTime)
        {
            _nextPerceptionTime = Time.time + _perceptionInterval;
            UpdatePerception();
        }

        if (_state == EnemyState.Idle || _state == EnemyState.Patrolling)
        {
            if (!_hasVisibleTarget && _patrol != null && _patrol.IsConfigured)
            {
                ChangeState(EnemyState.Patrolling);
                _patrol.UpdatePatrol();
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
            return;
        }

        if (_state == EnemyState.Attacking)
        {
            _motor.Stop();
            _attackSelector.TryAttack(_sensor.Target);
            return;
        }

        if (_state == EnemyState.Searching &&
            (Time.time - _lastSeenTime >= _memoryDuration || _motor.HasArrived))
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + _repathInterval;
            float? approachRange = _state == EnemyState.Chasing && _attackSelector.isActiveAndEnabled
                ? _attackSelector.GetApproachRange(_sensor.Target)
                : null;
            float? approachDistance = approachRange.HasValue
                ? Mathf.Max(0f, approachRange.Value - 0.2f)
                : (_state == EnemyState.Searching ? 0.1f : (float?)null);
            _motor.MoveTo(_lastKnownPosition, approachDistance);
        }
    }

    protected virtual void OnDisable()
    {
        _attackSelector.CancelCurrentAttack();
        _motor.Stop();
        ChangeState(EnemyState.Idle);
    }

    /// <summary>
    /// target을 센서에 전달하고 기존 기억과 경로를 초기화하여 새 대상의 감지를 시작한다.
    /// 추적 상태는 Idle로 변경되며 null 입력은 감지 대상을 제거한다.
    /// </summary>
    public void SetTarget(Transform target)
    {
        _sensor.SetTarget(target);
        _attackSelector.CancelCurrentAttack();
        _lastSeenTime = float.NegativeInfinity;
        _nextPerceptionTime = 0f;
        _hasVisibleTarget = false;
        _motor.Stop();
        ChangeState(EnemyState.Idle);
    }

    /// <summary>position에서 들린 검 소음을 duration 동안 기억하고 순찰을 중단해 소음 위치로 접근한다.</summary>
    public void HearNoise(Vector3 position, float duration)
    {
        if (!isActiveAndEnabled || _sensor.Target == null)
            return;
        _heardPosition = position;
        _noiseAlertUntil = Time.time + Mathf.Max(0.1f, duration);
        _lastKnownPosition = position;
        _lastSeenTime = Time.time;
        _nextPerceptionTime = 0f;
        if (!_attackSelector.IsBusy)
            ChangeState(EnemyState.Searching);
    }

    /// <summary>
    /// 센서의 대상 가시성과 기억 시간을 사용하여 추적 상태를 갱신한다.
    /// 보이는 대상의 마지막 위치와 시간을 기록하고 공격 범위에서는 공격 상태로 전환한다.
    /// 보이지 않는 대상은 수색 또는 대기로 전환한다.
    /// </summary>
    protected virtual void UpdatePerception()
    {
        _hasVisibleTarget = _sensor.CanSeeTarget();
        if (_sensor.Target == null || !_sensor.Target.gameObject.activeInHierarchy)
        {
            if (_state != EnemyState.Patrolling)
                ChangeState(EnemyState.Idle);
            return;
        }

        if (_hasVisibleTarget)
        {
            _lastKnownPosition = _sensor.Target.position;
            _lastSeenTime = Time.time;
            ChangeState(_attackSelector.isActiveAndEnabled &&
                _attackSelector.CanAttack(_sensor.Target) ? EnemyState.Attacking :
                    (AllowsWalkingChase ? EnemyState.Chasing : EnemyState.Idle));
            return;
        }

        if (Time.time < _noiseAlertUntil)
        {
            _lastKnownPosition = _heardPosition;
            _lastSeenTime = Time.time;
            ChangeState(EnemyState.Searching);
            return;
        }

        if (_state == EnemyState.Chasing || _state == EnemyState.Attacking)
            ChangeState(AllowsWalkingChase && _memoryDuration > 0f ? EnemyState.Searching : EnemyState.Idle);
    }

    /// <summary>
    /// nextState가 현재 상태와 다르면 상태를 변경하고 C# 이벤트와 파생 클래스의 상태 반응을 호출한다.
    /// Idle에서는 이동을 중지하고 Searching에서는 이전 경로의 도착 상태를 초기화한다.
    /// 다른 상태에서는 다음 경로 갱신을 즉시 허용한다.
    /// </summary>
    protected void ChangeState(EnemyState nextState)
    {
        if (_state == nextState)
            return;

        if (_state == EnemyState.Patrolling && _patrol != null)
            _patrol.Pause();
        _state = nextState;
        if (_state == EnemyState.Idle)
            _motor.Stop();
        else
        {
            if (_state == EnemyState.Searching)
                _motor.Stop();
            _nextRepathTime = 0f;
        }

        StateChanged?.Invoke(_state);
        OnStateChanged(_state);
    }

    /// <summary>
    /// nextState를 파생 유닛의 상태 반응 구현에 전달한다.
    /// 기본 구현은 상태를 추가로 변경하지 않으며 파생 클래스에서 애니메이션 등을 연결할 수 있다.
    /// </summary>
    protected virtual void OnStateChanged(EnemyState nextState)
    {
    }
}
