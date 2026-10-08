using UnityEngine;

using ReusableManagers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light), typeof(Poolable))]
public sealed class SensoryLight : SensoryEffect
{
    [Header("순간 빛")]
    [SerializeField] private Light _light;
    [SerializeField, Min(0.01f), Tooltip("순간 빛 표시 수명(초)")]
    private float _lifetime = 0.3f;
    private float _intensity;
    private bool _isConfigured;

    void Awake()
    {
        _isConfigured = _light != null && _light.type == LightType.Point;
        if (!_isConfigured)
        {
            Debug.LogError("SensoryLight에 Point Light를 연결해야 합니다.", this);
            return;
        }
        _intensity = _light.intensity;
        _light.enabled = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_isConfigured)
            _light.enabled = false;
    }

    /// <summary>
    /// 프리팹의 빛 속성과 수명에 공통 가시성을 적용하여 독립 재생한다.
    /// </summary>
    public void Play(float visibility = 1f)
    {
        if (!_isConfigured)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        _light.enabled = true;
        BeginPlayback(_lifetime, visibility);
    }

    /// <summary>
    /// 경과 시간과 외부 가시성으로 순간 빛의 강도를 페이드한다.
    /// </summary>
    protected override void ApplyFrame(float elapsed, float progress)
    {
        _light.intensity = _intensity * Visibility * (1f - progress);
    }
}
