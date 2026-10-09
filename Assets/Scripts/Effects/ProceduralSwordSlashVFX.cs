using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 원본 Slash 파티클의 메시 정점만 검 궤적으로 변형하고 원래 셰이더 입력과 연출을 유지한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SwordSlashTrail))]
public sealed class ProceduralSwordSlashVFX : MonoBehaviour
{
    [Header("원본 파티클")]
    [SerializeField] private ParticleSystem _slash;
    [SerializeField] private ParticleSystem _distortion;
    [SerializeField] private ParticleSystem _flying;

    [Header("베기 중 재생")]
    [SerializeField, Min(0.01f)] private float _attackPreviewTime = 0.08f;
    private readonly List<Vector3> _path = new List<Vector3>(256);
    private readonly ParticleSystem.Particle[] _particle = new ParticleSystem.Particle[1];
    private Mesh[] _meshes;
    private Mesh[] _originals;
    private ParticleSystem[] _surfaces;
    private ParticleSystemRenderer[] _renderers;
    private Vector3[][] _sourceVertices;
    private Vector3[][] _deformed;
    private Vector2[][] _sourceUvs;
    private float _previewTime;
    private readonly bool[] _emissionFinished = new bool[3];

    public bool IsAlive => _slash.IsAlive(false) || _distortion.IsAlive(false) || _flying.IsAlive(false);

    void Awake()
    {
        _surfaces = new[] { _slash, _distortion };
        _meshes = new Mesh[2];
        _originals = new Mesh[2];
        _renderers = new ParticleSystemRenderer[2];
        _sourceVertices = new Vector3[2][];
        _deformed = new Vector3[2][];
        _sourceUvs = new Vector2[2][];
        for (int i = 0; i < _surfaces.Length; i++)
        {
            _renderers[i] = _surfaces[i].GetComponent<ParticleSystemRenderer>();
            _originals[i] = _renderers[i].mesh;
            _sourceVertices[i] = _originals[i].vertices;
            _sourceUvs[i] = _originals[i].uv;
            _deformed[i] = new Vector3[_originals[i].vertexCount];
            _meshes[i] = Instantiate(_originals[i]);
            _meshes[i].name = "Procedural " + _originals[i].name;
            _meshes[i].MarkDynamic();
            _renderers[i].mesh = _meshes[i];
        }
        Clear();
    }

    void OnDestroy()
    {
        for (int i = 0; i < _meshes.Length; i++)
            Destroy(_meshes[i]);
    }

    /// <summary>
    /// 입력 없이 원본 세 파티클의 재생 시각을 초기화하고 첫 방출을 준비한다.
    /// 이전 공격의 입자를 지우고 베기 중 재생 시간을 0으로 설정한다.
    /// </summary>
    internal void Begin()
    {
        _previewTime = 0f;
        for (int i = 0; i < _emissionFinished.Length; i++)
            _emissionFinished[i] = false;
        _slash.Simulate(0f, false, true, false);
        _distortion.Simulate(0f, false, true, false);
        _flying.Simulate(0f, false, true, false);
    }

    /// <summary>
    /// 프레임 시간과 기록 상태로 원본 파티클 시뮬레이션을 진행한다.
    /// 베기 중 표면은 Preview Time까지 유지하고 종료 후 원본 수명 곡선을 끝까지 재생한다.
    /// </summary>
    internal void Tick(float deltaTime, bool recording)
    {
        float surfaceDelta = deltaTime;
        if (recording)
        {
            surfaceDelta = Mathf.Min(deltaTime, Mathf.Max(0f, _attackPreviewTime - _previewTime));
            _previewTime += surfaceDelta;
        }
        if (surfaceDelta > 0f)
        {
            Simulate(_slash, 0, surfaceDelta);
            Simulate(_distortion, 1, surfaceDelta);
        }
        Simulate(_flying, 2, deltaTime);
    }

    /// <summary>
    /// 파티클과 슬롯 번호·진행 시간으로 원본 시뮬레이션을 갱신한다.
    /// 방출 기간이 끝나면 방출을 정지해 수동 시뮬레이션의 대기 상태가 풀 반환을 막지 않게 한다.
    /// </summary>
    private void Simulate(ParticleSystem system, int index, float deltaTime)
    {
        if (_emissionFinished[index] && !system.IsAlive(false))
            return;
        system.Simulate(deltaTime, false, false, false);
        if (!_emissionFinished[index] && system.time >= system.main.duration)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            _emissionFinished[index] = true;
        }
    }

    /// <summary>
    /// 기록한 검 밑점·끝점의 띠 메시를 받아 원본 원호의 정점 위치만 변형한다.
    /// 원본 UV·삼각형·정점 색과 파티클의 Custom Data 스트림은 변경하지 않는다.
    /// </summary>
    internal void ApplyAppearance(Mesh path)
    {
        path.GetVertices(_path);
        int count = _path.Count / 2;
        for (int layer = 0; layer < _surfaces.Length; layer++)
        {
            bool visible = count >= 2 && _surfaces[layer].GetParticles(_particle) > 0;
            _renderers[layer].enabled = visible;
            if (!visible)
                continue;
            ParticleSystem.Particle particle = _particle[0];
            Matrix4x4 particleSpace = Matrix4x4.TRS(particle.position,
                Quaternion.Euler(particle.rotation3D), particle.GetCurrentSize3D(_surfaces[layer]));
            Matrix4x4 inverse = particleSpace.inverse;
            Vector3[] source = _sourceVertices[layer];
            Vector2[] uvs = _sourceUvs[layer];
            float maximumRadius = 0f;
            float minimumU = float.MaxValue;
            float maximumU = float.MinValue;
            for (int i = 0; i < source.Length; i++)
            {
                float radius = new Vector2(source[i].x, source[i].y).magnitude;
                maximumRadius = Mathf.Max(maximumRadius, radius);
                minimumU = Mathf.Min(minimumU, uvs[i].x);
                maximumU = Mathf.Max(maximumU, uvs[i].x);
            }
            for (int i = 0; i < source.Length; i++)
            {
                float progress = 1f - Mathf.InverseLerp(minimumU, maximumU, uvs[i].x);
                float sample = progress * (count - 1);
                int first = Mathf.Min(Mathf.FloorToInt(sample), count - 2);
                float blend = sample - first;
                Vector3 bladeBase = Vector3.Lerp(_path[first * 2], _path[(first + 1) * 2], blend);
                Vector3 bladeTip = Vector3.Lerp(_path[first * 2 + 1], _path[(first + 1) * 2 + 1], blend);
                Vector3 tangent = _path[(first + 1) * 2 + 1] - _path[first * 2 + 1];
                Vector3 normal = Vector3.Cross(bladeTip - bladeBase, tangent).normalized;
                float radius = new Vector2(source[i].x, source[i].y).magnitude;
                float width = radius / maximumRadius;
                float thickness = (bladeTip - bladeBase).magnitude / maximumRadius;
                Vector3 position = Vector3.Lerp(bladeBase, bladeTip, width) + normal * source[i].z * thickness;
                _deformed[layer][i] = inverse.MultiplyPoint3x4(transform.TransformPoint(position));
            }
            _meshes[layer].vertices = _deformed[layer];
            _meshes[layer].RecalculateNormals();
            _meshes[layer].RecalculateBounds();
        }
    }

    /// <summary>
    /// 입력 없이 원본 세 파티클을 정지하고 이전 공격의 입자를 제거한다.
    /// 표면 렌더러를 숨겨 취소·풀 반환 후 고정 원호가 나타나지 않게 한다.
    /// </summary>
    internal void Clear()
    {
        _slash.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _distortion.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        _flying.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_renderers == null)
            return;
        foreach (ParticleSystemRenderer renderer in _renderers)
            renderer.enabled = false;
    }
}
