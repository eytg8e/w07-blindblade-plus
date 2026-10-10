using UnityEngine;

[DisallowMultipleComponent]
public sealed class TerrainMemory : MonoBehaviour
{
    [Header("방문 지형 기억")]
    [SerializeField] private Transform _observer;
    [SerializeField] private Vector2 _minimum = new Vector2(-24f, -6f);
    [SerializeField] private Vector2 _size = new Vector2(48f, 76f);
    [SerializeField, Range(64, 512)] private int _resolution = 256;
    [SerializeField, Min(0.5f), Tooltip("방문으로 기억하는 수평 반경(미터)")]
    private float _radius = 5f;
    [SerializeField, Tooltip("기억으로만 보이는 지형 색상. 알파는 밝기 배율입니다.")]
    private Color _memoryColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    private Texture2D _memory;
    private Color32[] _pixels;
    private Vector3 _lastPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
    public Vector2 Minimum => _minimum;
    public Vector2 Size => _size;

    /// <summary>observer와 맵 범위를 연결하고 방문 기억 텍스처 및 셰이더 상태를 초기화한다.</summary>
    public void Initialize(Transform observer, Vector2 minimum, Vector2 size)
    {
        _observer = observer;
        _minimum = minimum;
        _size = size;
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
        if (_observer == null || (_observer.position - _lastPosition).sqrMagnitude < 0.04f)
            return;
        _lastPosition = _observer.position;
        Remember(_lastPosition);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat("_OutlineMemoryEnabled", 0f);
    }

    void OnDestroy()
    {
        Destroy(_memory);
    }

    /// <summary>position 주변의 격자를 방문 처리하고 씬 종료까지 누적 기억을 유지한다.</summary>
    private void Remember(Vector3 position)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt((position.x - _radius - _minimum.x) / _size.x * _resolution), 0, _resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((position.x + _radius - _minimum.x) / _size.x * _resolution), 0, _resolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((position.z - _radius - _minimum.y) / _size.y * _resolution), 0, _resolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((position.z + _radius - _minimum.y) / _size.y * _resolution), 0, _resolution - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = _minimum + new Vector2((x + 0.5f) / _resolution * _size.x, (y + 0.5f) / _resolution * _size.y);
                byte value = (byte)(255f * Mathf.Clamp01((_radius - Vector2.Distance(point, new Vector2(position.x, position.z))) / 0.75f));
                int index = y * _resolution + x;
                if (value > _pixels[index].r)
                    _pixels[index] = new Color32(value, value, value, 255);
            }
        }
        _memory.SetPixels32(_pixels);
        _memory.Apply(false);
    }
}
