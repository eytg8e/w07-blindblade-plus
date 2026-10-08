using UnityEngine;

namespace BlindBlade.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemySensor : MonoBehaviour
    {
        [Header("감지 설정")]
        [SerializeField] private Transform _target;
        [SerializeField, Min(0f)] private float _detectionRange = 12f;
        [SerializeField] private Vector3 _eyeOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;

        public Transform Target => _target;

        /// <summary>
        /// target을 감지 대상으로 지정하여 이후 거리와 시야 검사에 사용할 대상을 변경한다.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>
        /// 지정한 대상과 감지 거리, 시야 위치, 장애물 레이어를 사용하여 가시성을 검사한다.
        /// 활성 대상이 범위 안에 있고 시야가 열려 있으면 true를 반환한다.
        /// </summary>
        public bool CanSeeTarget()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
                return false;

            if ((_target.position - transform.position).sqrMagnitude > _detectionRange * _detectionRange)
                return false;

            Vector3 origin = transform.TransformPoint(_eyeOffset);
            Vector3 destination = _target.TransformPoint(_targetOffset);
            RaycastHit[] hits = Physics.RaycastAll(origin, destination - origin,
                Vector3.Distance(origin, destination), _obstacleMask, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(_target))
                    continue;

                return false;
            }

            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRange);
            if (_target != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.TransformPoint(_eyeOffset), _target.TransformPoint(_targetOffset));
            }
        }
    }
}
