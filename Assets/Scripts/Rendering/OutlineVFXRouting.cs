using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 카메라를 그리는 동안만 VFX 표시와 큐를 구분하고 이후 원본 상태를 복원한다.
/// </summary>
internal sealed class OutlineVFXRouting
{
    internal const uint SELECTED_MASK = 1u << 31;
    internal const uint ALWAYS_VISIBLE_MASK = 1u << 30;
    private readonly HashSet<Renderer> _selected = new HashSet<Renderer>();
    private readonly HashSet<Renderer> _alwaysVisible = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, Route> _cache = new Dictionary<Renderer, Route>();
    private readonly List<Route> _changed = new List<Route>();
    private readonly List<Renderer> _removed = new List<Renderer>();
    private readonly List<Material> _materials = new List<Material>();

    private sealed class Route
    {
        public Renderer Renderer { get; set; }
        public Material[] Originals { get; set; }
        public Material[] Copies { get; set; }
        public uint Mask { get; set; }
    }

    /// <summary>
    /// 컴포넌트로 선택한 렌더러와 기본 불투명 파티클을 카메라용으로 표시한다.
    /// 원본 조명용 렌더링 비트는 유지하며 선택한 효과만 임시 큐로 이동한다.
    /// </summary>
    public void Prepare()
    {
        Restore();
        OutlineVFX.Collect(_selected, _alwaysVisible);
        foreach (Renderer renderer in _selected)
            Apply(renderer, true);
        foreach (ParticleSystemRenderer renderer in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
        {
            if (_selected.Contains(renderer) || !renderer.enabled || !renderer.gameObject.scene.IsValid())
                continue;
            renderer.GetSharedMaterials(_materials);
            bool opaque = false;
            foreach (Material material in _materials)
                opaque |= material != null && material.renderQueue <= 2500;
            if (opaque)
                Apply(renderer, false);
        }
        _removed.Clear();
        foreach (var item in _cache)
            if (item.Key == null)
                _removed.Add(item.Key);
        foreach (Renderer renderer in _removed)
        {
            DestroyCopies(_cache[renderer]);
            _cache.Remove(renderer);
        }
    }

    /// <summary>
    /// renderer의 머티리얼과 마스크를 기억하고 selected 여부에 맞춰 임시 표시한다.
    /// 투명 셰이더의 합성 설정은 유지하며 큐만 윤곽선 처리 이전으로 옮긴다.
    /// </summary>
    private void Apply(Renderer renderer, bool selected)
    {
        renderer.GetSharedMaterials(_materials);
        if (!_cache.TryGetValue(renderer, out Route route))
        {
            route = new Route { Renderer = renderer };
            _cache.Add(renderer, route);
        }
        bool changed = route.Originals == null || route.Originals.Length != _materials.Count;
        if (!changed)
            for (int i = 0; i < _materials.Count; i++)
                changed |= route.Originals[i] != _materials[i];
        if (changed)
        {
            DestroyCopies(route);
            route.Originals = _materials.ToArray();
            route.Copies = new Material[_materials.Count];
        }
        route.Mask = renderer.renderingLayerMask;
        if (route.Originals.Length > 0)
        {
            for (int i = 0; i < route.Originals.Length; i++)
            {
                Material original = route.Originals[i];
                if (original == null)
                    continue;
                if (route.Copies[i] == null)
                    route.Copies[i] = new Material(original) { hideFlags = HideFlags.HideAndDontSave };
                route.Copies[i].shader = original.shader;
                route.Copies[i].CopyPropertiesFromMaterial(original);
                route.Copies[i].renderQueue = selected ? 2500
                    : (original.renderQueue <= 2500 ? 3000 : original.renderQueue);
                if (selected && original.renderQueue > 2500)
                {
                    // 원래 투명 재질은 불투명 깊이·노멀 프리패스에서 제외한다.
                    route.Copies[i].SetShaderPassEnabled("DepthOnly", false);
                    route.Copies[i].SetShaderPassEnabled("DepthNormals", false);
                    route.Copies[i].SetShaderPassEnabled("DepthNormalsOnly", false);
                }
            }
            renderer.sharedMaterials = route.Copies;
        }
        renderer.renderingLayerMask = selected ? route.Mask | SELECTED_MASK : route.Mask;
        if (selected && _alwaysVisible.Contains(renderer))
            renderer.renderingLayerMask |= ALWAYS_VISIBLE_MASK;
        _changed.Add(route);
    }

    /// <summary>
    /// 이 카메라에서 바꾼 렌더러의 원본 머티리얼과 마스크를 복원한다.
    /// 저장 및 다음 카메라에는 임시 큐나 표시 비트를 남기지 않는다.
    /// </summary>
    public void Restore()
    {
        foreach (Route route in _changed)
        {
            if (route.Renderer == null)
                continue;
            route.Renderer.sharedMaterials = route.Originals;
            route.Renderer.renderingLayerMask = route.Mask;
        }
        _changed.Clear();
    }

    /// <summary>
    /// route의 임시 머티리얼만 파괴한다. 원본 에셋은 유지한다.
    /// </summary>
    private static void DestroyCopies(Route route)
    {
        if (route.Copies == null)
            return;
        foreach (Material material in route.Copies)
            UnityEngine.Rendering.CoreUtils.Destroy(material);
    }

    /// <summary>
    /// 렌더러 상태를 복원하고 모든 캐시 머티리얼을 해제한다.
    /// </summary>
    public void Dispose()
    {
        Restore();
        foreach (Route route in _cache.Values)
            DestroyCopies(route);
        _cache.Clear();
    }
}
