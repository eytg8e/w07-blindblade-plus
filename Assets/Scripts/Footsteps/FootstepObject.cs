using System.Collections;

using UnityEngine;

using ReusableManagers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Poolable))]
public sealed class FootstepObject : MonoBehaviour
{
    [Header("발자국 표시")]
    [SerializeField] private SpriteRenderer _leftRenderer;
    [SerializeField] private SpriteRenderer _rightRenderer;
    private Color _leftColor;
    private Color _rightColor;
    private SpriteRenderer _activeRenderer;
    private Color _activeColor;

    [Header("수명")]
    [SerializeField, Min(0.01f), Tooltip("발자국 표시 수명(초)")]
    private float _lifetime = 3f;
    [SerializeField, Min(0.01f), Tooltip("수명 종료 직전 페이드 시간(초)")]
    private float _fadeDuration = 1f;
    private Coroutine _lifetimeRoutine;
    private Transform _observer;
    private float _visibleDistance;
    private float _maximumDistance;
    private bool _isConfigured;

    void Awake()
    {
        _isConfigured = _leftRenderer != null && _rightRenderer != null
            && _leftRenderer != _rightRenderer;
        if (!_isConfigured)
        {
            Debug.LogError("FootstepObject에 서로 다른 좌/우 렌더러를 연결해야 합니다.", this);
            return;
        }
        _leftColor = _leftRenderer.color;
        _rightColor = _rightRenderer.color;
    }

    void OnDisable()
    {
        if (_lifetimeRoutine != null)
        {
            StopCoroutine(_lifetimeRoutine);
            _lifetimeRoutine = null;
        }
        _observer = null;
        if (_isConfigured)
        {
            _leftRenderer.enabled = false;
            _rightRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 좌우 선택과 관측자 거리 설정을 받아 발자국 표시 수명을 시작한다.
    /// </summary>
    public void Play(bool isLeft, Transform observer, float visibleDistance, float maximumDistance)
    {
        if (!_isConfigured)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        if (_lifetimeRoutine != null)
            StopCoroutine(_lifetimeRoutine);
        _observer = observer;
        _visibleDistance = visibleDistance;
        _maximumDistance = maximumDistance;
        _leftRenderer.enabled = isLeft;
        _rightRenderer.enabled = !isLeft;
        _activeRenderer = isLeft ? _leftRenderer : _rightRenderer;
        _activeColor = isLeft ? _leftColor : _rightColor;
        _lifetimeRoutine = StartCoroutine(AnimateLifetime());
    }

    /// <summary>
    /// 수명과 현재 관측자 거리에 따라 발자국 알파를 갱신하고 종료 시 풀로 반환한다.
    /// </summary>
    private IEnumerator AnimateLifetime()
    {
        float elapsed = 0f;
        float lifetime = Mathf.Max(0.01f, _lifetime);
        float fadeDuration = Mathf.Clamp(_fadeDuration, 0.01f, lifetime);
        while (elapsed < lifetime)
        {
            float visibility = _observer != null
                ? CalculateVisibility(Vector3.Distance(transform.position, _observer.position),
                    _visibleDistance, _maximumDistance)
                : 0f;
            float fade = Mathf.Clamp01((lifetime - elapsed) / fadeDuration);
            Color color = _activeColor;
            color.a *= visibility * fade;
            _activeRenderer.color = color;
            yield return null;
            elapsed += Time.deltaTime;
        }
        _lifetimeRoutine = null;
        Managers.Resource.Destroy(gameObject);
    }

    /// <summary>
    /// 관측 거리와 완전 표시 및 최대 거리를 사용해 0에서 1 사이의 가시성을 반환한다.
    /// </summary>
    public static float CalculateVisibility(float distance, float visibleDistance, float maximumDistance)
    {
        if (distance >= maximumDistance)
            return 0f;
        if (distance <= visibleDistance)
            return 1f;
        return 1f - Mathf.InverseLerp(visibleDistance, maximumDistance, distance);
    }
}
