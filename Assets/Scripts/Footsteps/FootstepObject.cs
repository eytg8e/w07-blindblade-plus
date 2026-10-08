using UnityEngine;

using ReusableManagers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Poolable))]
public sealed class FootstepObject : SensoryEffect
{
    [Header("발자국 표시")]
    [SerializeField] private SpriteRenderer _leftRenderer;
    [SerializeField] private SpriteRenderer _rightRenderer;
    private Color _leftColor;
    private Color _rightColor;
    private SpriteRenderer _activeRenderer;
    private Color _activeColor;
    private bool _isConfigured;

    [Header("수명")]
    [SerializeField, Min(0.01f), Tooltip("직접 재생할 때의 표시 수명(초)")]
    private float _lifetime = 3f;
    [SerializeField, Min(0.01f), Tooltip("직접 재생할 때의 종료 직전 페이드 시간(초)")]
    private float _fadeDuration = 1f;
    private float _playingLifetime;
    private float _playingFade;

    void Awake()
    {
        _isConfigured = _leftRenderer != null && _rightRenderer != null && _leftRenderer != _rightRenderer;
        if (!_isConfigured)
        {
            Debug.LogError("FootstepObject에 서로 다른 좌/우 렌더러를 연결해야 합니다.", this);
            return;
        }
        _leftColor = _leftRenderer.color;
        _rightColor = _rightRenderer.color;
        _leftRenderer.enabled = false;
        _rightRenderer.enabled = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_isConfigured)
        {
            _leftRenderer.enabled = false;
            _rightRenderer.enabled = false;
        }
        _activeRenderer = null;
    }

    /// <summary>
    /// 좌우 선택과 가시성에 인스펙터의 기본 수명을 적용해 발자국을 재생한다.
    /// </summary>
    public void Play(bool isLeft, float visibility = 1f)
    {
        Play(isLeft, visibility, _lifetime, _fadeDuration);
    }

    /// <summary>
    /// 좌우 선택과 공통 가시성 및 수명 설정으로 독립 발자국 재생을 시작한다.
    /// </summary>
    public void Play(bool isLeft, float visibility, float lifetime, float fadeDuration)
    {
        if (!_isConfigured)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        _leftRenderer.enabled = isLeft;
        _rightRenderer.enabled = !isLeft;
        _activeRenderer = isLeft ? _leftRenderer : _rightRenderer;
        _activeColor = isLeft ? _leftColor : _rightColor;
        _playingLifetime = Mathf.Max(0.01f, lifetime);
        _playingFade = Mathf.Clamp(fadeDuration, 0.01f, _playingLifetime);
        BeginPlayback(_playingLifetime, visibility);
    }

    /// <summary>
    /// 경과 시간과 외부 가시성을 사용하여 선택한 발자국의 페이드 알파를 갱신한다.
    /// </summary>
    protected override void ApplyFrame(float elapsed, float progress)
    {
        Color color = _activeColor;
        color.a *= Visibility * Mathf.Clamp01((_playingLifetime - elapsed) / _playingFade);
        _activeRenderer.color = color;
    }
}
