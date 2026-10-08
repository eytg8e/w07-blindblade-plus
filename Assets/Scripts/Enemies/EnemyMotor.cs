using UnityEngine;
using UnityEngine.AI;

namespace BlindBlade.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
        [SerializeField, Min(0.01f)] private float _acceleration = 8f;
        [SerializeField, Min(0f)] private float _angularSpeed = 360f;
        [SerializeField, Min(0f)] private float _stoppingDistance = 1.5f;
        [SerializeField, Min(0.01f)] private float _destinationSampleRadius = 2f;
        private NavMeshAgent _agent;
        private Vector3 _destination;
        private bool _hasDestination;

        public bool IsReady => _agent.isActiveAndEnabled && _agent.isOnNavMesh;
        public bool HasArrived => IsReady && _hasDestination && !_agent.pathPending &&
            (IsWithinStoppingDistance(_destination) ||
                (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete &&
                    _agent.remainingDistance <= _agent.stoppingDistance + 0.1f));

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = _moveSpeed;
            _agent.acceleration = _acceleration;
            _agent.angularSpeed = _angularSpeed;
            _agent.stoppingDistance = _stoppingDistance;
            _agent.autoRepath = true;
        }

        /// <summary>
        /// destination 주변의 같은 에이전트 유형과 허용 영역에 있는 NavMesh 위치로 이동한다.
        /// stoppingDistance가 있으면 해당 거리로 정지하며 없으면 Inspector 설정을 사용한다.
        /// 경로 요청을 수락하면 true를 반환하고 이동을 재개하며, 실패하면 경로를 제거한다.
        /// </summary>
        public bool MoveTo(Vector3 destination, float? stoppingDistance = null)
        {
            if (!IsReady)
                return false;

            _agent.stoppingDistance = stoppingDistance.HasValue
                ? Mathf.Max(0f, stoppingDistance.Value)
                : _stoppingDistance;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, _destinationSampleRadius, filter))
            {
                Stop();
                return false;
            }

            _destination = hit.position;
            _hasDestination = true;
            if (IsWithinStoppingDistance(_destination))
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                return true;
            }

            _agent.isStopped = false;
            if (_agent.SetDestination(hit.position))
                return true;

            Stop();
            return false;
        }

        /// <summary>
        /// 현재 NavMeshAgent의 경로와 속도를 초기화하여 이동을 중지한다.
        /// Inspector 입력을 사용하지 않으며 에이전트가 NavMesh 위에 있을 때만 상태를 변경한다.
        /// </summary>
        public void Stop()
        {
            _hasDestination = false;
            if (!IsReady)
                return;

            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        /// <summary>
        /// destination과 현재 위치의 거리를 에이전트 정지 거리 및 도착 허용 오차와 비교한다.
        /// 거리 안에 있으면 true를 반환하여 반복 경로 요청으로 대상에 과도하게 접근하지 않게 한다.
        /// </summary>
        private bool IsWithinStoppingDistance(Vector3 destination)
        {
            float distance = _agent.stoppingDistance + 0.1f;
            return (destination - transform.position).sqrMagnitude <= distance * distance;
        }
    }
}
