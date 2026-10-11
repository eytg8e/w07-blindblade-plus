using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 이 오브젝트와 자식의 VFX 렌더러를 선택적 윤곽선 처리 대상으로 등록한다.
/// 원본 머티리얼과 충돌 레이어는 변경하지 않는다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Effects/Outline VFX")]
public sealed class OutlineVFX : MonoBehaviour
{
    private static readonly HashSet<OutlineVFX> _active = new HashSet<OutlineVFX>();

    [Header("인지 표시")]
    [SerializeField] private bool _ignorePerception;
    internal bool IgnorePerception { get => _ignorePerception; set => _ignorePerception = value; }
    internal bool IgnoreOcclusion { get; set; }

    void OnEnable() { _active.Add(this); }
    void OnDisable() { _active.Remove(this); }

    /// <summary>
    /// 활성 컴포넌트의 렌더러를 result에, 인지 예외를 alwaysVisible에, 차폐 예외를 overlay에 모은다.
    /// 동적 자식도 포함하며 같은 렌더러는 한 번만 등록한다.
    /// </summary>
    internal static void Collect(HashSet<Renderer> result, HashSet<Renderer> alwaysVisible, HashSet<Renderer> overlay)
    {
        result.Clear();
        alwaysVisible.Clear();
        overlay.Clear();
        foreach (OutlineVFX marker in _active)
        {
            if (marker == null || !marker.isActiveAndEnabled || !marker.gameObject.scene.IsValid())
                continue;
            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>())
                if (renderer.enabled)
                {
                    result.Add(renderer);
                    if (marker._ignorePerception)
                        alwaysVisible.Add(renderer);
                    if (marker.IgnoreOcclusion)
                        overlay.Add(renderer);
                }
        }
    }
}
