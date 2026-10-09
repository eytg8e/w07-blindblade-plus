using UnityEngine;
using UnityEngine.AI;

namespace BlindBlade.Enemies
{
    [RequireComponent(typeof(EnemyMotor))]
    public class EnemyChargeAttack : EnemyAttackBehaviour
    {
        [Header("돌진 설정")]
        [SerializeField, Min(0.1f)] private float _attackRange = 10f;
        [SerializeField, Min(0.1f)] private float _chargeSpeed = 15f;
        [SerializeField, Min(0.1f)] private float _chargeDuration = 0.6f;
        [SerializeField] private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;
        private NavMeshAgent _agent;
        private EnemyMotor _motor;
        private float _previousSpeed;
        private float _previousAcceleration;
        private float _previousStoppingDistance;
        private bool _isCharging;

        public override float Range => _attackRange;
        public override bool AllowsMovement => Phase == EnemyAttackPhase.Active;
        public bool IsMoving => Phase == EnemyAttackPhase.Active && _isCharging && !_agent.isStopped;

        protected override float ActiveDuration => _chargeDuration;
        protected override EnemySoundAction SoundAction => EnemySoundAction.Charge;

        protected override void Awake()
        {
            base.Awake();
            _agent = GetComponent<NavMeshAgent>();
            _motor = GetComponent<EnemyMotor>();
        }

        /// <summary>
        /// target의 사거리와 직선 NavMesh 경로를 검사하여 돌진 가능한 경우 true를 반환한다.
        /// 벽이나 이동 불가능한 경계가 경로를 막으면 다른 공격 패턴을 선택하도록 false를 반환한다.
        /// </summary>
        public override bool CanAttack(Transform target)
        {
            if (!base.CanAttack(target) || !_motor.IsReady)
                return false;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };
            return NavMesh.SamplePosition(target.position, out NavMeshHit sampled, 1f, filter) &&
                !NavMesh.Raycast(_agent.nextPosition, sampled.position, out _, filter);
        }

        /// <summary>
        /// context의 고정 조준 방향을 사용하여 장애물과 NavMesh 경계 전까지 돌진 경로를 요청한다.
        /// 에이전트의 이동 설정을 저장하고 돌진 속도로 변경하며 유효한 목적지가 없으면 취소한다.
        /// </summary>
        protected override void PerformAttack(EnemyAttackContext context)
        {
            if (!_motor.IsReady)
            {
                CancelAttack();
                return;
            }

            Vector3 direction = context.Direction;
            direction.y = 0f;
            direction.Normalize();
            float distance = Mathf.Min(_attackRange, Vector3.Distance(context.Origin, context.AimPosition));
            Vector3 origin = transform.position + Vector3.up * (_agent.height * 0.5f);
            RaycastHit[] hits = Physics.SphereCastAll(origin, _agent.radius, direction, distance,
                _obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform) ||
                    (context.Target != null && hit.transform.IsChildOf(context.Target)))
                    continue;

                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.1f));
            }

            Vector3 destination = transform.position + direction * distance;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };

            if (!NavMesh.SamplePosition(destination, out NavMeshHit sampled, 1f, filter))
            {
                CancelAttack();
                return;
            }

            if (NavMesh.Raycast(_agent.nextPosition, sampled.position, out NavMeshHit boundary, filter))
                destination = boundary.position;
            else
                destination = sampled.position;

            _previousSpeed = _agent.speed;
            _previousAcceleration = _agent.acceleration;
            _previousStoppingDistance = _agent.stoppingDistance;
            _isCharging = true;
            _agent.speed = _chargeSpeed;
            _agent.acceleration = _chargeSpeed / 0.05f;
            _agent.stoppingDistance = 0f;
            _agent.isStopped = false;
            if (direction.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(direction);
            if (!_agent.SetDestination(destination))
                CancelAttack();
        }

        /// <summary>
        /// context로 시작한 돌진이 목적지에 도착했는지 검사한다.
        /// 도착했으면 에이전트를 정지시키며 공격 실행 시간 동안 추가 추적 경로를 요청하지 않는다.
        /// </summary>
        protected override void UpdateActiveAttack(EnemyAttackContext context)
        {
            if (_motor.IsReady && !_agent.pathPending && _agent.remainingDistance <= 0.1f)
                _motor.Stop();
        }

        /// <summary>
        /// context의 실행 종료 또는 cancelled에 따른 취소 시 돌진 이전의 이동 설정을 복원한다.
        /// 돌진 중일 때만 경로를 정지하고 속도·가속도·정지 거리를 원래 값으로 되돌린다.
        /// </summary>
        protected override void OnAttackEnded(EnemyAttackContext context, bool cancelled)
        {
            if (!_isCharging)
                return;

            _isCharging = false;
            _motor.Stop();
            _agent.speed = _previousSpeed;
            _agent.acceleration = _previousAcceleration;
            _agent.stoppingDistance = _previousStoppingDistance;
        }
    }
}
