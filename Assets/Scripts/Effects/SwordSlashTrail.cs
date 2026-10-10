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
    [SerializeField, Min(0.01f)] private float _lifetime = 0.18f;
    [SerializeField, Range(4, 128)] private int _maximumSamples = 64;
    [SerializeField, Min(0.001f)] private float _minimumDistance = 0.015f;
    [SerializeField, Min(0.01f)] private float _teleportDistance = 3f;
    [SerializeField, ColorUsage(true, true)] private Color _color = new Color(0.85f, 0.88f, 0.9f, 0.8f);
    private readonly List<BladeSample> _samples = new List<BladeSample>(128);
    private readonly List<Vector3> _vertices = new List<Vector3>(256);
    private readonly List<Vector2> _uvs = new List<Vector2>(256);
    private readonly List<Color> _colors = new List<Color>(256);
    private readonly List<int> _triangles = new List<int>(762);
    private Mesh _mesh;
    private bool _emitting;

    public bool IsPlaying => _emitting || _samples.Count > 0;

    private readonly struct BladeSample
    {
        public Vector3 Base { get; }
        public Vector3 Tip { get; }
        public float Time { get; }

        /// <summary>
        /// 검 밑점·끝점의 월드 위치와 게임 시각을 받아 불변 궤적 표본을 만든다.
        /// 각 입력값을 표본의 위치와 생성 시각으로 보관한다.
        /// </summary>
        public BladeSample(Vector3 bladeBase, Vector3 bladeTip, float time)
        {
            Base = bladeBase;
            Tip = bladeTip;
            Time = time;
        }
    }

    void Awake()
    {
        _mesh = new Mesh { name = "Procedural Sword Slash Trail" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
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
            if (distance >= _teleportDistance)
                _samples.Clear();
            else if (distance < _minimumDistance)
                return;
        }
        if (_samples.Count >= _maximumSamples)
            _samples.RemoveAt(0);
        _samples.Add(new BladeSample(bladeBase, bladeTip, Time.time));
    }

    /// <summary>
    /// 유효한 월드 위치 표본을 현재 로컬 좌표로 변환해 양면 띠 메시를 갱신한다.
    /// 표본의 경과 시간과 색상으로 정점 알파를 줄이고 메시 경계를 재계산한다.
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
            _vertices.Add(transform.InverseTransformPoint(sample.Tip));
            float progress = i / (float)Mathf.Max(1, _samples.Count - 1);
            _uvs.Add(new Vector2(progress, 0f));
            _uvs.Add(new Vector2(progress, 1f));
            Color color = _color;
            color.a *= Mathf.Pow(Mathf.Clamp01(1f - (Time.time - sample.Time) / _lifetime), 1.5f);
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
    }
}
