using System.Collections.Generic;

using UnityEngine;

namespace BlindBlade.Rendering
{
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

        void OnEnable() { _active.Add(this); }
        void OnDisable() { _active.Remove(this); }

        /// <summary>
        /// 활성 컴포넌트의 월드 렌더러를 result에 모은다.
        /// 동적 자식도 포함하며 같은 렌더러는 한 번만 등록한다.
        /// </summary>
        internal static void Collect(HashSet<Renderer> result)
        {
            result.Clear();
            foreach (OutlineVFX marker in _active)
            {
                if (marker == null || !marker.isActiveAndEnabled || !marker.gameObject.scene.IsValid())
                    continue;
                foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled)
                        result.Add(renderer);
            }
        }
    }
}
