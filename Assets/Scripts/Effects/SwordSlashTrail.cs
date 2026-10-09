using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 검 밑점과 끝점의 월드 위치를 기록해 시간에 따라 사라지는 띠 메시를 생성한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SwordSlashTrail : MonoBehaviour
{
    [Header("검 연결")]
    [SerializeField] private Transform _bladeBase;
    [SerializeField] private Transform _bladeTip;

    [Header("궤적")]
    [SerializeField, Min(0.01f)] private float _lifetime = 0.28f;
    [SerializeField, Range(4, 128)] private int _maximumSamples = 128;
    [SerializeField, Min(0.001f)] private float _minimumDistance = 0.015f;
    [SerializeField, Min(0.01f)] private float _teleportDistance = 3f;
    [SerializeField, Range(1f, 2f)] private float _widthScale = 1.3f;
    [SerializeField, Min(0.01f)] private float _patternLength = 4f;
    [SerializeField, ColorUsage(true, true)] private Color _color = Color.white;
    private readonly List<BladeSample> _samples = new List<BladeSample>(128);
    private readonly List<Vector3> _vertices = new List<Vector3>(256);
    private readonly List<Vector2> _uvs = new List<Vector2>(256);
    private readonly List<Color> _colors = new List<Color>(256);
    private readonly List<int> _triangles = new List<int>(762);
    private Mesh _mesh;
    private bool _emitting;
    private float _patternDistance;
    private ProceduralSwordSlashVFX _appearance;

    public bool IsPlaying => _emitting || _samples.Count > 0
        || (_appearance != null && _appearance.IsAlive);

    private readonly struct BladeSample
    {
        public Vector3 Base { get; }
        public Vector3 Tip { get; }
        public float Time { get; }
        public float PatternDistance { get; }

        /// <summary>
        /// 검 양 끝점·게임 시각·누적 이동 거리로 불변 궤적 표본을 만든다.
        /// 위치·생성 시각·무늬 좌표를 보관해 이후 표본 추가에도 무늬가 이동하지 않게 한다.
        /// </summary>
        public BladeSample(Vector3 bladeBase, Vector3 bladeTip, float time, float patternDistance)
        {
            Base = bladeBase;
            Tip = bladeTip;
            Time = time;
            PatternDistance = patternDistance;
        }
    }

    void Awake()
    {
        _mesh = new Mesh { name = "Procedural Sword Slash Trail" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _appearance = GetComponent<ProceduralSwordSlashVFX>();
    }

    void LateUpdate()
    {
        if (Time.deltaTime <= 0f || (!_emitting && _samples.Count == 0))
            return;
        if (_emitting && (_bladeBase == null || _bladeTip == null
            || !_bladeBase.gameObject.activeInHierarchy || !_bladeTip.gameObject.activeInHierarchy))
            _emitting = false;
        if (_emitting)
            RecordSample();
        if (_appearance != null)
        {
            _appearance.Tick(Time.deltaTime, _emitting);
            if (!_emitting && !_appearance.IsAlive)
                _samples.Clear();
        }
        else
            while (_samples.Count > 0 && Time.time - _samples[0].Time >= _lifetime)
                _samples.RemoveAt(0);
        RebuildMesh();
    }

    void OnDisable()
    {
        Clear();
    }

    void OnDestroy()
    {
        Destroy(_mesh);
    }

    /// <summary>
    /// Inspector에 연결된 검 양 끝점으로 새 궤적을 시작한다.
    /// 기존 기록을 제거하고 위치 기록 상태를 활성화한다.
    /// </summary>
    public void BeginTrail()
    {
        BeginTrail(_bladeBase, _bladeTip);
    }

    /// <summary>
    /// 실제 검 밑점과 끝점 Transform을 받아 새로운 궤적을 시작한다.
    /// 참조를 교체하고 이전 메시를 제거한 뒤 첫 월드 위치를 기록한다.
    /// </summary>
    public void BeginTrail(Transform bladeBase, Transform bladeTip)
    {
        Clear();
        _bladeBase = bladeBase;
        _bladeTip = bladeTip;
        _emitting = true;
        if (_appearance != null)
        {
            transform.position = bladeBase.position;
            _appearance.Begin();
        }
        RecordSample();
    }

    /// <summary>
    /// 현재 검 위치를 마지막으로 기록한 뒤 위치 기록을 중단한다.
    /// 입력값 없이 기존 궤적은 각 표본의 수명이 끝날 때까지 유지한다.
    /// </summary>
    public void EndTrail()
    {
        if (_emitting && _bladeBase != null && _bladeTip != null)
            RecordSample();
        _emitting = false;
    }

    /// <summary>
    /// 풀 반환 시 외부 검 Transform 참조와 잔상을 모두 해제한다.
    /// 입력값 없이 이전 공격의 검 참조를 제거해 다음 재생에 남기지 않는다.
    /// </summary>
    internal void ReleaseBlade()
    {
        Clear();
        _bladeBase = null;
        _bladeTip = null;
    }

    /// <summary>
    /// 외부 입력 없이 표본·메시를 제거하고 검 궤적 기록을 중단한다.
    /// 비활성화·재사용 시 이전 공격의 궤적을 남기지 않는다.
    /// </summary>
    public void Clear()
    {
        _emitting = false;
        _samples.Clear();
        _patternDistance = 0f;
        if (_appearance != null)
            _appearance.Clear();
        if (_mesh != null)
            _mesh.Clear();
    }

    /// <summary>
    /// 연결된 검 Transform의 현재 월드 위치를 거리 기준으로 기록한다.
    /// 순간이동이면 이전 표본을 제거하고 최대 표본 수를 유지한다.
    /// </summary>
    private void RecordSample()
    {
        Vector3 bladeBase = _bladeBase.position;
        Vector3 bladeTip = _bladeTip.position;
        if (_samples.Count > 0)
        {
            BladeSample previous = _samples[_samples.Count - 1];
            float distance = Mathf.Max(Vector3.Distance(previous.Base, bladeBase),
                Vector3.Distance(previous.Tip, bladeTip));
            float teleportMovement = _appearance != null
                ? Vector3.Distance(previous.Base, bladeBase) : distance;
            if (teleportMovement >= _teleportDistance)
            {
                _samples.Clear();
                _patternDistance = 0f;
            }
            else if (distance < _minimumDistance)
                return;
            else
                _patternDistance += distance;
        }
        if (_samples.Count >= _maximumSamples)
            _samples.RemoveAt(0);
        _samples.Add(new BladeSample(bladeBase, bladeTip, Time.time, _patternDistance));
    }

    /// <summary>
    /// 월드 표본과 시각적 폭 배율로 검 궤적의 양면 띠 메시를 갱신한다.
    /// 누적 거리로 고정 UV를 만들고 경과 시간을 정점 알파로 전달해 투명도를 줄인다.
    /// </summary>
    private void RebuildMesh()
    {
        _mesh.Clear();
        _vertices.Clear();
        _uvs.Clear();
        _colors.Clear();
        _triangles.Clear();
        for (int i = 0; i < _samples.Count; i++)
        {
            BladeSample sample = _samples[i];
            _vertices.Add(transform.InverseTransformPoint(sample.Base));
            Vector3 visualTip = sample.Base + (sample.Tip - sample.Base) * _widthScale;
            _vertices.Add(transform.InverseTransformPoint(visualTip));
            float progress = sample.PatternDistance / _patternLength;
            _uvs.Add(new Vector2(progress, 0f));
            _uvs.Add(new Vector2(progress, 1f));
            Color color = _color;
            color.a *= Mathf.Clamp01(1f - (Time.time - sample.Time) / _lifetime);
            _colors.Add(color);
            _colors.Add(color);
            if (i == 0)
                continue;
            int vertex = i * 2;
            _triangles.Add(vertex - 2);
            _triangles.Add(vertex - 1);
            _triangles.Add(vertex);
            _triangles.Add(vertex);
            _triangles.Add(vertex - 1);
            _triangles.Add(vertex + 1);
        }
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
        if (_appearance != null)
            _appearance.ApplyAppearance(_mesh);
    }
}
