using System;

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace BlindBlade.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyFootsteps : MonoBehaviour
    {
        [Header("발걸음 설정")]
        [SerializeField] private bool _useAnimationEvents;
        [SerializeField, Min(0.01f)] private float _stepDistance = 1.2f;
        [SerializeField, Min(0f)] private float _minimumSpeed = 0.1f;
        [SerializeField, Min(0.01f)] private float _teleportDistance = 5f;
        private NavMeshAgent _agent;
        private Vector3 _previousPosition;
        private float _distanceSinceStep;

        [Header("발걸음 이벤트")]
        [SerializeField] private UnityEvent<Vector3> _onFootstep = new UnityEvent<Vector3>();

        public event Action<Vector3> Footstep;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        void OnEnable()
        {
            _previousPosition = transform.position;
            _distanceSinceStep = 0f;
        }

        void LateUpdate()
        {
            Vector3 displacement = transform.position - _previousPosition;
            _previousPosition = transform.position;
            displacement.y = 0f;
            float distance = displacement.magnitude;

            if (_useAnimationEvents || !IsWalking() || distance > _teleportDistance)
            {
                _distanceSinceStep = 0f;
                return;
            }

            _distanceSinceStep += distance;
            if (_distanceSinceStep >= _stepDistance)
            {
                _distanceSinceStep %= _stepDistance;
                EmitFootstep(transform.position);
            }
        }

        /// <summary>
        /// 애니메이션 이벤트 모드와 현재 이동 상태를 확인하여 루트 위치의 발걸음 이벤트를 발생시킨다.
        /// 입력값 없이 사운드와 발자국 수신자에게 현재 위치를 전달한다.
        /// </summary>
        public void NotifyFootstep()
        {
            if (_useAnimationEvents && IsWalking())
                EmitFootstep(transform.position);
        }

        /// <summary>
        /// 활성 상태, NavMesh 상태, 속도 설정을 검사하여 유닛이 걷고 있으면 true를 반환한다.
        /// 에이전트가 멈췄거나 이동 속도가 minimumSpeed 이하이면 false를 반환한다.
        /// </summary>
        private bool IsWalking()
        {
            return isActiveAndEnabled && _agent.isActiveAndEnabled && _agent.isOnNavMesh &&
                !_agent.isStopped && _agent.velocity.sqrMagnitude > _minimumSpeed * _minimumSpeed;
        }

        /// <summary>
        /// position을 C# 이벤트와 Inspector 이벤트에 전달하여 발걸음 효과의 확장 지점을 호출한다.
        /// 이동 상태를 변경하지 않고 연결된 사운드 및 발자국 수신자에 위치를 알린다.
        /// </summary>
        private void EmitFootstep(Vector3 position)
        {
            Footstep?.Invoke(position);
            _onFootstep.Invoke(position);
        }
    }
}
