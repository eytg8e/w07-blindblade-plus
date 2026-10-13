using System;

using UnityEngine;

/// <summary>
/// DefenseTest의 대나무숲에 임의 방향의 흰 윤곽선 바람 파도를 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DefensePocBambooWind : MonoBehaviour
{
    private static readonly int _waveOpacityId = Shader.PropertyToID("_WaveOpacity");

    [Header("연결")]
    [SerializeField] private Transform _forest;
    [SerializeField] private Material _outlineMaterial;

    [Header("바람 파도")]
    [SerializeField, Min(0.01f)] private float _interval = 5f;
    [SerializeField, Min(0.01f)] private float _travelDuration = 2f;
    [SerializeField, Range(0.01f, 0.8f)] private float _brightCoverage = 0.3f;
    [SerializeField, Range(0.01f, 0.35f)] private float _shoulderCoverage = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _shoulderOpacity = 0.5f;
    [SerializeField, Min(0.1f)] private float _forestOuterRadius = 22f;
    private MeshRenderer[] _bamboo;
    private float[] _projectionDistances;
    private MaterialPropertyBlock _properties;
    private Vector2 _direction;
    private float _brightHalfWidth;
    private float _outerHalfWidth;
    private float _cycleElapsed;
    private float _waveElapsed;
    private bool _waveActive;

    void Awake()
    {
        _bamboo = _forest.GetComponentsInChildren<MeshRenderer>();
        _projectionDistances = new float[_bamboo.Length];
        _properties = new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in _bamboo)
        {
            renderer.sharedMaterial = _outlineMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }
    }

    void Update()
    {
        _cycleElapsed += Time.deltaTime;
        if (_cycleElapsed >= _interval)
        {
            _cycleElapsed -= _interval;
            BeginWave();
        }
        if (!_waveActive) return;

        _waveElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_waveElapsed / _travelDuration);
        float head = Mathf.Lerp(-_forestOuterRadius - _outerHalfWidth,
            _forestOuterRadius + _outerHalfWidth, progress);
        foreach (MeshRenderer renderer in _bamboo)
        {
            Vector3 position = renderer.transform.position - _forest.position;
            float coordinate = position.x * _direction.x + position.z * _direction.y;
            float distance = Mathf.Abs(coordinate - head);
            float opacity = GetOpacity(distance);
            bool visible = opacity > 0.01f;
            if (renderer.enabled != visible) renderer.enabled = visible;
            if (!visible) continue;
            _properties.SetFloat(_waveOpacityId, opacity);
            renderer.SetPropertyBlock(_properties);
        }

        if (progress < 1f) return;
        _waveActive = false;
        foreach (MeshRenderer renderer in _bamboo)
            renderer.enabled = false;
    }

    void OnDisable()
    {
        if (_bamboo == null) return;
        foreach (MeshRenderer renderer in _bamboo)
            if (renderer != null) renderer.enabled = false;
    }

    /// <summary>
    /// 새 임의 수평 방향을 골라 게임 시간 기준 2초 파도를 시작한다.
    /// 방향별 대나무 투영 거리를 정렬해 중심 30%와 양쪽 15%의 폭을 저장한다.
    /// </summary>
    private void BeginWave()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        _direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        for (int index = 0; index < _bamboo.Length; index++)
        {
            Vector3 position = _bamboo[index].transform.position - _forest.position;
            _projectionDistances[index] = Mathf.Abs(position.x * _direction.x
                + position.z * _direction.y);
        }
        Array.Sort(_projectionDistances);
        _brightHalfWidth = GetCoverageDistance(_brightCoverage);
        _outerHalfWidth = GetCoverageDistance(_brightCoverage + 2f * _shoulderCoverage);
        _waveElapsed = 0f;
        _waveActive = true;
    }

    /// <summary>
    /// coverage 비율에 해당하는 정렬된 대나무 투영 거리 값을 반환한다.
    /// 바람 띠의 중앙과 주변 면적 비율을 실제 줄기 배치에 맞춘다.
    /// </summary>
    private float GetCoverageDistance(float coverage)
    {
        int index = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(coverage)
            * _projectionDistances.Length) - 1, 0, _projectionDistances.Length - 1);
        return _projectionDistances[index];
    }

    /// <summary>
    /// distance가 중심 띠에 있으면 1, 주변 띠에 있으면 shoulderOpacity를 반환한다.
    /// 두 경계만 부드럽게 전환하며 바깥쪽은 0으로 숨긴다.
    /// </summary>
    private float GetOpacity(float distance)
    {
        float feather = Mathf.Min(0.35f, (_outerHalfWidth - _brightHalfWidth) * 0.1f);
        float brightBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            _brightHalfWidth - feather, _brightHalfWidth + feather, distance));
        float outerBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            _outerHalfWidth - feather, _outerHalfWidth + feather, distance));
        return Mathf.Lerp(1f, _shoulderOpacity, brightBlend) * (1f - outerBlend);
    }
}
