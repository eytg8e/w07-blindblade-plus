using System.Collections.Generic;

using UnityEngine;

[DisallowMultipleComponent]
public sealed class TerrainMemory : MonoBehaviour
{
    private const float MEMORY_EDGE_FADE = 0.75f;

    [Header("방문 지형 기억")]
    [SerializeField] private Transform _observer;
    [SerializeField] private Vector2 _minimum = new Vector2(-24f, -6f);
    [SerializeField] private Vector2 _size = new Vector2(200f, 200f);
    [SerializeField, Range(64, 512)] private int _resolution = 512;
    [SerializeField, Min(0.5f), Tooltip("방문으로 기억하는 수평 반경(미터)")]
    private float _radius = 5f;
    [SerializeField, Tooltip("기억으로만 보이는 지형 색상. 알파는 밝기 배율입니다.")]
    private Color _memoryColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("런타임 상태")]
    private Texture2D _memory;
    private Color32[] _pixels;
    private readonly List<ExpandingWaveMemory> _expandingWaves = new List<ExpandingWaveMemory>(8);
    private Vector3 _lastPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
    public Vector2 Minimum => _minimum;
    public Vector2 Size => _size;

    private sealed class ExpandingWaveMemory
    {
        public readonly Vector3 Center;
        public readonly float StartRadius;
        public readonly float EndRadius;
        public readonly float Duration;
        public readonly float StartedAt;

        /// <summary>
        /// 파동 중심과 확산 반경, 지속 시간 및 시작 시각을 보관한다.
        /// </summary>
        public ExpandingWaveMemory(Vector3 center, float startRadius, float endRadius, float duration, float startedAt)
        {
            Center = center;
            StartRadius = startRadius;
            EndRadius = endRadius;
            Duration = duration;
            StartedAt = startedAt;
        }
    }

    /// <summary>observer와 맵 범위를 연결하고 방문 기억 텍스처 및 셰이더 상태를 초기화한다.</summary>
    public void Initialize(Transform observer, Vector2 minimum, Vector2 size)
    {
        _observer = observer;
        _minimum = minimum;
        _size = size;
        _expandingWaves.Clear();
        if (_memory != null) Destroy(_memory);
        _lastPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
        _memory = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false, true);
        _memory.name = "Visited terrain";
        _memory.wrapMode = TextureWrapMode.Clamp;
        _memory.filterMode = FilterMode.Bilinear;
        _pixels = new Color32[_resolution * _resolution];
        _memory.SetPixels32(_pixels);
        _memory.Apply(false);
        ApplyShaderState();
    }

    /// <summary>초기화한 기억 텍스처와 범위·색상을 전역 셰이더에 적용한다.</summary>
    private void ApplyShaderState()
    {
        Shader.SetGlobalTexture("_OutlineTerrainMemory", _memory);
        Shader.SetGlobalVector("_OutlineMemoryBounds", new Vector4(_minimum.x, _minimum.y, _size.x, _size.y));
        Shader.SetGlobalFloat("_OutlineMemoryEnabled", 1f);
        Shader.SetGlobalColor("_OutlineMemoryColor", _memoryColor);
    }

    void LateUpdate()
    {
        if (_memory == null) return;
        Shader.SetGlobalColor("_OutlineMemoryColor", _memoryColor);
        bool changed = false;
        if (_observer != null)
        {
            if ((_observer.position - _lastPosition).sqrMagnitude >= 0.04f)
            {
                _lastPosition = _observer.position;
                changed |= Remember(_lastPosition, _radius);
            }
        }
        changed |= UpdateExpandingWaves();
        if (changed)
        {
            _memory.SetPixels32(_pixels);
            _memory.Apply(false);
        }
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat("_OutlineMemoryEnabled", 0f);
    }

    void OnDestroy()
    {
        Destroy(_memory);
    }

    /// <summary>
    /// 파동 확산 정보를 등록해 지나간 영역을 기억 텍스처에 점진적으로 누적한다.
    /// startRadius, endRadius, duration을 사용하며 씬 기억은 유지한다.
    /// </summary>
    public void RememberExpandingWave(Vector3 center, float startRadius, float endRadius, float duration)
    {
        if (endRadius <= 0f || duration <= 0f)
            return;

        _expandingWaves.Add(new ExpandingWaveMemory(
            center,
            Mathf.Max(0f, startRadius),
            endRadius,
            duration,
            Time.time));
    }

    /// <summary>
    /// 활성 파동의 현재 반경을 기억 격자에 누적하고 확산이 끝난 파동을 제거한다.
    /// </summary>
    private bool UpdateExpandingWaves()
    {
        bool changed = false;
        float currentTime = Time.time;
        for (int i = _expandingWaves.Count - 1; i >= 0; i--)
        {
            ExpandingWaveMemory wave = _expandingWaves[i];
            float progress = Mathf.Clamp01((currentTime - wave.StartedAt) / wave.Duration);
            float expansion = Mathf.SmoothStep(0f, 1f, progress);
            float radius = Mathf.Lerp(wave.StartRadius, wave.EndRadius, expansion);
            changed |= Remember(wave.Center, radius);
            if (progress >= 1f)
                _expandingWaves.RemoveAt(i);
        }

        return changed;
    }

    /// <summary>
    /// position 중심의 radius 영역을 XZ 격자에 더 밝게 누적하고 변경 여부를 반환한다.
    /// </summary>
    private bool Remember(Vector3 position, float radius)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt((position.x - radius - _minimum.x) / _size.x * _resolution), 0, _resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((position.x + radius - _minimum.x) / _size.x * _resolution), 0, _resolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((position.z - radius - _minimum.y) / _size.y * _resolution), 0, _resolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((position.z + radius - _minimum.y) / _size.y * _resolution), 0, _resolution - 1);
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = _minimum + new Vector2((x + 0.5f) / _resolution * _size.x, (y + 0.5f) / _resolution * _size.y);
                byte value = (byte)(255f * Mathf.Clamp01((radius - Vector2.Distance(point, new Vector2(position.x, position.z))) / MEMORY_EDGE_FADE));
                int index = y * _resolution + x;
                if (value > _pixels[index].r)
                {
                    _pixels[index] = new Color32(value, value, value, 255);
                    changed = true;
                }
            }
        }

        return changed;
    }
}
