using System;

using UnityEngine;

/// <summary>
/// 이동하는 오브젝트에 부착되어 이동 거리를 누적 감지하고 지면에 발자국을 생성한다.
/// </summary>
public class FootstepEmitter : MonoBehaviour
{
    public static event Action<Vector3, float> OnFootstepEmitted;

    [Header("Footstep Prefab")]
    [SerializeField] private GameObject _footstepPrefab;
    [SerializeField] private string _footstepResourcePath = "Prefabs/Footstep";

    [Header("Step Settings")]
    [SerializeField] private float _stepDistance = 1.2f;
    [SerializeField] private float _footSpacing = 0.25f;
    [SerializeField] private LayerMask _groundLayer = ~0;
    [SerializeField] private float _rayDistance = 1.5f;
    [SerializeField] private float _rayHeightOffset = 0.5f;

    [Header("Visibility & Distance")]
    [SerializeField] private bool _enableFootsteps = true;
    [SerializeField] private Transform _targetObserver;
    [SerializeField] private float _minVisibilityDistance = 15f;
    [SerializeField] private float _maxVisibilityDistance = 25f;
    [SerializeField] private float _waveRadius = 2.5f;

    private Vector3 _lastPosition;
    private float _accumulatedDistance;
    private bool _isNextLeftFoot = true;

    public bool EnableFootsteps
    {
        get => _enableFootsteps;
        set => _enableFootsteps = value;
    }

    public Transform TargetObserver
    {
        get => _targetObserver;
        set => _targetObserver = value;
    }

    void Start()
    {
        _lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;
        float moveDelta = Vector3.Distance(currentPosition, _lastPosition);

        // 움직임이 감지되면 이동 거리를 누적한다.
        if (moveDelta > 0.001f)
        {
            _accumulatedDistance += moveDelta;
            _lastPosition = currentPosition;

            if (_accumulatedDistance >= _stepDistance)
            {
                _accumulatedDistance = 0f;
                TryEmitFootstep();
            }
        }
    }

    /// <summary>
    /// 지면을 향해 레이를 발사하고 관측자와의 거리에 따라 발자국을 스폰한다.
    /// 누적된 위치와 방향을 사용하여 좌/우 오프셋 및 가시성 알파를 계산한다.
    /// </summary>
    public void TryEmitFootstep()
    {
        if (!_enableFootsteps)
            return;

        Vector3 observerPos = GetObserverPosition();
        float distanceToObserver = Vector3.Distance(transform.position, observerPos);

        // 최대 가시 거리를 초과하면 발자국 생성을 생략한다.
        if (distanceToObserver > _maxVisibilityDistance)
            return;

        // 거리에 따른 가시성 알파 비율 계산 (최소 가시 거리 이내면 1.0, 그 이상이면 점진적 감쇠)
        float visibilityAlpha = 1f;
        if (distanceToObserver > _minVisibilityDistance)
        {
            float range = _maxVisibilityDistance - _minVisibilityDistance;
            if (range > 0f)
            {
                visibilityAlpha = 1f - Mathf.Clamp01((distanceToObserver - _minVisibilityDistance) / range);
            }
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        float lateralOffset = _isNextLeftFoot ? -_footSpacing : _footSpacing;
        Vector3 stepOrigin = transform.position + (right * lateralOffset) + (Vector3.up * _rayHeightOffset);

        if (Physics.Raycast(stepOrigin, Vector3.down, out RaycastHit hit, _rayDistance, _groundLayer))
        {
            Vector3 spawnPosition = hit.point + (hit.normal * 0.005f);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, hit.normal), hit.normal);

            SpawnFootstep(spawnPosition, spawnRotation, _isNextLeftFoot, visibilityAlpha);

            // 외부 파동 연동을 위한 이벤트 발행
            OnFootstepEmitted?.Invoke(spawnPosition, _waveRadius);

            _isNextLeftFoot = !_isNextLeftFoot;
        }
    }

    /// <summary>
    /// 발자국 프리팹을 인스턴스화하고 상태를 초기화한다.
    /// position, rotation, isLeft, visibilityAlpha를 인자로 받아 컴포넌트를 구성한다.
    /// </summary>
    private void SpawnFootstep(Vector3 position, Quaternion rotation, bool isLeft, float visibilityAlpha)
    {
        GameObject footstepObj = null;

        if (Managers.IsInitialized)
        {
            if (_footstepPrefab != null)
            {
                footstepObj = Managers.Resource.Instantiate(_footstepPrefab, position, rotation);
            }
            else if (!string.IsNullOrEmpty(_footstepResourcePath))
            {
                footstepObj = Managers.Resource.Instantiate(_footstepResourcePath, position, rotation);
            }
        }
        else
        {
            if (_footstepPrefab != null)
            {
                footstepObj = Instantiate(_footstepPrefab, position, rotation);
            }
        }

        if (footstepObj == null)
            return;

        FootstepItem item = footstepObj.GetComponent<FootstepItem>();
        if (item != null)
        {
            item.Init(isLeft, visibilityAlpha, _waveRadius);
        }
    }

    /// <summary>
    /// 관측자 위치를 반환한다. 별도 지정이 없으면 메인 카메라 위치를 기준으로 한다.
    /// 대상 Transform이 설정되어 있으면 해당 좌표를 반환하고, 없을 경우 Camera.main을 탐색한다.
    /// </summary>
    private Vector3 GetObserverPosition()
    {
        if (_targetObserver != null)
            return _targetObserver.position;

        Camera mainCam = Camera.main;
        if (mainCam != null)
            return mainCam.transform.position;

        return transform.position;
    }
}
