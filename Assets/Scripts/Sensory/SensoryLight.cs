using UnityEngine;

using ReusableManagers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light), typeof(Poolable))]
public sealed class SensoryLight : SensoryEffect
{
    [Header("순간 빛")]
    [SerializeField] private Light _light;
    private float _intensity;
    private float _lifetime;
    private float _fadeDuration;
    private bool _isConfigured;

    void Awake()
    {
        _isConfigured = _light != null && _light.type == LightType.Point;
        if (!_isConfigured)
        {
            Debug.LogError("SensoryLight에 Point Light를 연결해야 합니다.", this);
            return;
        }
        _light.enabled = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_isConfigured)
            _light.enabled = false;
    }

    /// <summary>
    /// 공통 가시성과 빛 색상·강도·범위·수명으로 독립적인 순간 빛을 재생한다.
    /// </summary>
    public void Play(float visibility, Color color, float intensity, float range, float lifetime, float fadeDuration)
    {
        if (!_isConfigured)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        _light.color = color;
        _light.range = range;
        _intensity = intensity;
        _lifetime = Mathf.Max(0.01f, lifetime);
        _fadeDuration = Mathf.Clamp(fadeDuration, 0.01f, _lifetime);
        _light.enabled = true;
        BeginPlayback(_lifetime, visibility);
    }

    /// <summary>
    /// 경과 시간과 외부 가시성으로 순간 빛의 강도를 페이드한다.
    /// </summary>
    protected override void ApplyFrame(float elapsed, float progress)
    {
        _light.intensity = _intensity * Visibility * Mathf.Clamp01((_lifetime - elapsed) / _fadeDuration);
    }
}
