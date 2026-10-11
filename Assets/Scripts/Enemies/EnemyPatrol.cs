using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMotor), typeof(NavMeshAgent))]
public sealed class EnemyPatrol : MonoBehaviour
{
    [Header("왕복 순찰")]
    [SerializeField, Min(0f), Tooltip("배치 위치부터 순찰 끝점까지의 편도 거리입니다. 0이면 순찰하지 않습니다.")]
    private float _distance = 3f;
    [SerializeField, Tooltip("배치된 적의 로컬 좌표 기준 순찰 방향입니다.")]
    private Vector3 _localDirection = Vector3.forward;
    [SerializeField, Min(0f)] private float _waitDuration = 1f;
    private EnemyMotor _motor;
    private NavMeshAgent _agent;
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private bool _goingOut;
    private bool _hasDestination;
    private float _nextMoveTime;

    public bool IsConfigured => isActiveAndEnabled && _distance > 0f && (_endPosition - _startPosition).sqrMagnitude > 0.01f;

    void Awake()
    {
        _motor = GetComponent<EnemyMotor>();
        _agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        _startPosition = transform.position;
        Vector3 direction = transform.TransformDirection(_localDirection);
        direction.y = 0f;
        _endPosition = _startPosition + direction.normalized * _distance;
        _goingOut = true;
        _hasDestination = false;
        _nextMoveTime = 0f;
    }

    void OnDisable()
    {
        Pause();
    }

    /// <summary>
    /// 설정된 두 끝점과 대기 시간을 사용하여 순찰 경로를 요청하고 도착 시 다음 끝점으로 전환한다.
    /// Controller가 미발견 상태에서만 호출하며 도달 불가능한 경로는 정지 후 반대 지점을 시도한다.
    /// </summary>
    public void UpdatePatrol()
    {
        if (!IsConfigured || !_motor.IsReady || Time.time < _nextMoveTime)
            return;

        if (_hasDestination)
        {
            bool blocked = !_agent.pathPending &&
                (_agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                (_agent.pathStatus == NavMeshPathStatus.PathPartial && _agent.remainingDistance <= 0.1f));
            if (_motor.HasArrived || blocked)
                CompleteLeg();
            return;
        }

        _hasDestination = _motor.MoveTo(_goingOut ? _endPosition : _startPosition, 0.05f);
        if (!_hasDestination)
            CompleteLeg();
    }

    /// <summary>
    /// 입력값 없이 현재 순찰 경로를 정지하고 재개 시 같은 끝점으로 이동할 수 있도록 상태를 초기화한다.
    /// 시작 위치와 진행 방향은 유지하여 추적 이후 기존 순찰 구간으로 돌아가게 한다.
    /// </summary>
    public void Pause()
    {
        _motor.Stop();
        _hasDestination = false;
        _nextMoveTime = 0f;
    }

    /// <summary>
    /// 현재 이동을 종료하고 설정된 대기 시간을 예약한 뒤 다음 왕복 방향으로 전환한다.
    /// 이동 경로를 정지하며 다음 순찰 목적지는 반대 끝점이 된다.
    /// </summary>
    private void CompleteLeg()
    {
        _motor.Stop();
        _hasDestination = false;
        _goingOut = !_goingOut;
        _nextMoveTime = Time.time + _waitDuration;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? _startPosition : transform.position;
        Vector3 direction = transform.TransformDirection(_localDirection);
        direction.y = 0f;
        Vector3 end = Application.isPlaying ? _endPosition : start + direction.normalized * _distance;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.25f);
        Gizmos.DrawWireSphere(end, 0.25f);
    }
}
