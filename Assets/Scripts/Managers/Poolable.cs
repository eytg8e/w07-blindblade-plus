using UnityEngine;

namespace ReusableManagers
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Reusable Managers/Poolable")]
    public sealed class Poolable : MonoBehaviour
    {
        public bool IsUsing { get; internal set; }
        internal PoolManager Owner { get; set; }
        internal GameObject Original { get; set; }
    }
}
