using System;

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyMotor))]
public class EnemyRhythmChargeAttack : EnemyAttackBehaviour
{
    private const int PULSE_COUNT = 4;

    [Header("박자 돌진 설정")]
    [SerializeField, Min(0.1f)] private float _chargeSpeed = 12f;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.16f;
    [SerializeField, Min(0f)] private float _beatPause = 0.38f;
    [SerializeField, Min(0f)] private float _doublePause = 0.06f;
    [SerializeField, Min(0.1f)] private float _stepDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float _finalStepDistance = 1.8f;
    [SerializeField, Tooltip("전조 때의 대상 위치까지 거리를 네 이동에 배분합니다. Step Distance와 Final Step Distance는 거리 비율로 사용합니다.")]
    private bool _scaleToTargetDistance = true;
    [SerializeField] private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;
    private NavMeshAgent _agent;
    private EnemyMotor _motor;
    private EnemySensor _rangeSensor;
    private Vector3 _direction;
    private Vector3 _lockedDestination;
    private float _previousSpeed;
    private float _previousAcceleration;
    private float _previousStoppingDistance;
    private float _pulseEndTime;
    private float _nextPulseTime;
    private int _pulseIndex;
    private bool _isRunning;
    private bool _isMoving;

    public override float Range => _rangeSensor.DetectionRange;
    public override bool AllowsMovement => Phase == EnemyAttackPhase.Active;
    public bool IsMovingPulse => Phase == EnemyAttackPhase.Active && _isMoving;
    public event Action<EnemyAttackContext, int> PulseStarted;

    protected override EnemySoundAction SoundAction => EnemySoundAction.RhythmCharge;
    protected override float ActiveDuration => _pulseDuration * PULSE_COUNT + _beatPause * 2f + _doublePause + 0.1f;
    protected override bool HasFinishedActiveAttack => _pulseIndex == PULSE_COUNT - 1 && Time.time >= _pulseEndTime;

    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
        _motor = GetComponent<EnemyMotor>();
        _rangeSensor = GetComponent<EnemySensor>();
    }

    /// <summary>
    /// target의 사거리와 같은 유형의 NavMesh 직선 경로를 검사하여 사용 가능하면 true를 반환한다.
    /// 대상 루트의 높이 대신 에이전트 지면 높이를 사용하여 몸 중심 피벗도 감지한다.
    /// 경로가 장애물이나 경계로 막히거나 이동 준비가 안 되어 있으면 false를 반환한다.
    /// </summary>
    public override bool CanAttack(Transform target)
    {
        if (!base.CanAttack(target) || !_motor.IsReady)
            return false;

        NavMeshQueryFilter filter = GetNavigationFilter();
        Vector3 groundTarget = new Vector3(target.position.x, _agent.nextPosition.y, target.position.z);
        return NavMesh.SamplePosition(groundTarget, out NavMeshHit sampled, 1f, filter) &&
            !NavMesh.Raycast(_agent.nextPosition, sampled.position, out _, filter);
    }

    /// <summary>
    /// context의 고정 조준 방향으로 첫 돌진을 시작하고 기존 이동 설정을 저장한다.
    /// 이동이 불가능하거나 수평 방향이 없으면 공격을 취소한다.
    /// </summary>
    protected override void PerformAttack(EnemyAttackContext context)
    {
        _direction = context.Direction;
        _direction.y = 0f;
        _direction.Normalize();
        _lockedDestination = context.AimPosition;
        if (!_motor.IsReady || _direction.sqrMagnitude == 0f)
        {
            CancelAttack();
            return;
        }

        _previousSpeed = _agent.speed;
        _previousAcceleration = _agent.acceleration;
        _previousStoppingDistance = _agent.stoppingDistance;
        _isRunning = true;
        _pulseIndex = 0;
        _agent.speed = _chargeSpeed;
        _agent.acceleration = _chargeSpeed / 0.05f;
        _agent.stoppingDistance = 0f;
        transform.rotation = Quaternion.LookRotation(_direction);
        BeginPulse(context);
    }

    /// <summary>
    /// context로 시작한 돌진의 이동 종료와 다음 박자 예약을 매 프레임 처리한다.
    /// 네 번의 이동을 세 박자 그룹으로 실행하며 쉬는 구간과 마지막 이동 종료 후에는 정지한다.
    /// </summary>
    protected override void UpdateActiveAttack(EnemyAttackContext context)
    {
        if (!_motor.IsReady)
        {
            CancelAttack();
            return;
        }

        if (_isMoving && (Time.time >= _pulseEndTime ||
            (!_agent.pathPending && _agent.remainingDistance <= 0.1f)))
        {
            _motor.Stop();
            _isMoving = false;
        }

        if (_pulseIndex < PULSE_COUNT - 1 && Time.time >= _nextPulseTime)
        {
            _pulseIndex++;
            BeginPulse(context);
        }
    }

    /// <summary>
    /// context와 현재 이동 번호로 고정 조준 위치까지 거리를 배분하고 장애물 앞까지 목적지를 제한한다.
    /// 첫 이동의 소리는 공통 실행 호출을 사용하며 나머지는 이동 직전에 같은 공통 소리를 요청한다.
    /// </summary>
    private void BeginPulse(EnemyAttackContext context)
    {
        _motor.Stop();
        float distance = _pulseIndex < 2 ? _stepDistance : _finalStepDistance;
        if (_scaleToTargetDistance)
        {
            float remainingDistance = Mathf.Max(0f, Vector3.Dot(_lockedDestination - _agent.nextPosition, _direction));
            float remainingWeight = _pulseIndex < 2
                ? _stepDistance * (2 - _pulseIndex) + _finalStepDistance * 2f
                : _finalStepDistance * (PULSE_COUNT - _pulseIndex);
            distance = remainingDistance * distance / remainingWeight;
        }
        Vector3 origin = transform.position + Vector3.up * (_agent.height * 0.5f);
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, _agent.radius, _direction,
            distance, _obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) ||
                (context.Target != null && hit.transform.IsChildOf(context.Target)))
                continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.1f));
        }

        Vector3 destination = _agent.nextPosition + _direction * distance;
        if (NavMesh.Raycast(_agent.nextPosition, destination, out NavMeshHit boundary, GetNavigationFilter()))
            destination = Vector3.MoveTowards(boundary.position, _agent.nextPosition, 0.1f);

        if (_pulseIndex > 0)
        {
            SoundId soundId = EnemySoundSettings.GetSound(SoundAction, EnemySoundStage.Action);
            if (soundId != SoundId.None)
                Managers.Sound.PlayAttached(soundId, gameObject);
        }

        _pulseEndTime = Time.time + _pulseDuration;
        float pause = _pulseIndex == 2 ? _doublePause : _beatPause;
        _nextPulseTime = _pulseEndTime + pause;
        _isMoving = distance > 0.1f && (destination - _agent.nextPosition).sqrMagnitude > 0.01f;
        if (_isMoving)
        {
            _agent.speed = _scaleToTargetDistance
                ? Mathf.Max(_chargeSpeed, distance / Mathf.Max(0.01f, _pulseDuration - 0.05f))
                : _chargeSpeed;
            _agent.acceleration = _agent.speed / 0.05f;
            _agent.isStopped = false;
            if (!_agent.SetDestination(destination))
            {
                CancelAttack();
                return;
            }
        }
        PulseStarted?.Invoke(context, _pulseIndex);
    }

    /// <summary>
    /// 현재 에이전트의 유형과 허용 영역을 사용하는 NavMesh 필터를 반환한다.
    /// 에이전트 설정을 변경하지 않고 감지와 각 이동의 경계 검사에 사용한다.
    /// </summary>
    private NavMeshQueryFilter GetNavigationFilter()
    {
        return new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
    }

    /// <summary>
    /// context의 실행 종료 또는 cancelled 취소 시 이동을 중지하고 저장한 이동 설정을 복원한다.
    /// 박자 진행 상태를 초기화하여 비활성화 및 다음 공격에서 남은 이동이 실행되지 않게 한다.
    /// </summary>
    protected override void OnAttackEnded(EnemyAttackContext context, bool cancelled)
    {
        if (!_isRunning)
            return;
        _isRunning = false;
        _isMoving = false;
        _motor.Stop();
        _agent.speed = _previousSpeed;
        _agent.acceleration = _previousAcceleration;
        _agent.stoppingDistance = _previousStoppingDistance;
    }
}
