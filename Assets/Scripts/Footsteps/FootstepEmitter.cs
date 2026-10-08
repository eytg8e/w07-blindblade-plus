using System;

using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootstepEmitter : MonoBehaviour
{
    public event Action<Transform, Vector3> OnFootstepEmitted;
    [Header("연결")]
    [SerializeField] private FootstepObject _footstepPrefab;
    [SerializeField, Tooltip("가시성 기준 플레이어 또는 카메라")]
    private Transform _observer;

    [Header("걸음")]
    [SerializeField, Min(0.01f), Tooltip("발자국 사이 이동 거리(미터)")]
    private float _stepDistance = 0.7f;
    [SerializeField, Min(0f), Tooltip("중심에서 각 발까지의 거리(미터)")]
    private float _footOffset = 0.15f;
    [SerializeField, Min(0.01f), Tooltip("프레임 이동이 이 거리 이상이면 순간이동으로 처리(미터)")]
    private float _teleportDistance = 5f;
    private Vector3 _previousPosition;
    private float _accumulatedDistance;
    private bool _isLeft = true;

    [Header("지면")]
    [SerializeField] private LayerMask _groundLayers;
    [SerializeField, Min(0f), Tooltip("발 위치 위에서 레이를 시작하는 높이(미터)")]
    private float _rayHeight = 0.5f;
    [SerializeField, Min(0.01f), Tooltip("발 위치 아래까지 검사할 깊이(미터)")]
    private float _rayDepth = 0.5f;
    [SerializeField, Min(0f), Tooltip("지면 겹침을 방지할 법선 방향 간격(미터)")]
    private float _surfaceOffset = 0.01f;

    [Header("관측 거리")]
    [SerializeField, Min(0f), Tooltip("완전히 표시하는 거리(미터)")]
    private float _visibleDistance = 5f;
    [SerializeField, Min(0.01f), Tooltip("생성과 표시를 중단하는 거리(미터)")]
    private float _maximumDistance = 15f;

    private void OnEnable()
    {
        _previousPosition = transform.position;
        _accumulatedDistance = 0f;
        _isLeft = true;
    }

    private void Start()
    {
        if (_footstepPrefab == null || _observer == null || _groundLayers.value == 0
            || _stepDistance <= 0f || _maximumDistance <= _visibleDistance)
        {
            Debug.LogError("FootstepEmitter의 프리팹, 관측자, 지면 레이어 및 거리 설정을 확인해야 합니다.", this);
            enabled = false;
            return;
        }
        Managers.Pool.CreatePool(_footstepPrefab.gameObject);
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        Vector3 previousPosition = _previousPosition;
        _previousPosition = currentPosition;
        Vector3 movement = currentPosition - previousPosition;
        if (movement.magnitude >= _teleportDistance)
        {
            _accumulatedDistance = 0f;
            return;
        }
        movement.y = 0f;
        float distance = movement.magnitude;
        if (distance <= Mathf.Epsilon)
            return;
        if (_observer == null)
        {
            Debug.LogError("FootstepEmitter의 관측자가 파괴되었습니다. 발자국 생성을 중단합니다.", this);
            enabled = false;
            return;
        }
        Vector3 direction = movement / distance;
        float stepDistance = Mathf.Max(0.01f, _stepDistance);
        float nextDistance = stepDistance - _accumulatedDistance;
        while (nextDistance <= distance)
        {
            Vector3 stepPosition = Vector3.Lerp(previousPosition, currentPosition, nextDistance / distance);
            EmitFootstep(stepPosition, direction);
            nextDistance += stepDistance;
        }
        _accumulatedDistance = Mathf.Repeat(_accumulatedDistance + distance, stepDistance);
    }

    /// <summary>
    /// 이동 경로의 발 위치와 진행 방향으로 지면을 검사하고 발자국 생성 및 위치 이벤트를 발행한다.
    /// </summary>
    private void EmitFootstep(Vector3 position, Vector3 direction)
    {
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        position += right * (_isLeft ? -_footOffset : _footOffset);
        bool isLeft = _isLeft;
        _isLeft = !_isLeft;
        
        if (!Physics.Raycast(position + Vector3.up * _rayHeight, Vector3.down,
                out RaycastHit hit, _rayHeight + _rayDepth, _groundLayers,
                QueryTriggerInteraction.Ignore))
            return;

        OnFootstepEmitted?.Invoke(transform, hit.point);
        if (FootstepObject.CalculateVisibility(Vector3.Distance(hit.point, _observer.position),
                _visibleDistance, _maximumDistance) <= 0f)
            return;
        Vector3 forward = Vector3.ProjectOnPlane(direction, hit.normal).normalized;
        if (forward.sqrMagnitude < 0.001f)
            return;
        Quaternion rotation = Quaternion.LookRotation(forward, hit.normal);
        GameObject instance = Managers.Resource.Instantiate(_footstepPrefab.gameObject,
            hit.point + hit.normal * _surfaceOffset, rotation);
        if (instance == null)
            return;
        instance.GetComponent<FootstepObject>().Play(isLeft, _observer, _visibleDistance, _maximumDistance);
    }
}
