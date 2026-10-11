using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PoolManager
{
    private sealed class Pool
    {
        internal GameObject Original;
        internal Transform Root;
        internal readonly Stack<Poolable> Idle = new Stack<Poolable>();
        internal readonly HashSet<Poolable> All = new HashSet<Poolable>();
    }

    private readonly Dictionary<GameObject, Pool> _pools = new Dictionary<GameObject, Pool>();
    private readonly Transform _host;
    private Transform _root;
    private bool _clearing;

    internal PoolManager(Transform host)
    {
        _host = host;
    }

    public void CreatePool(GameObject original, int count = 5)
    {
        if (original == null || _clearing)
            return;
        Pool pool = GetOrCreate(original);
        pool.All.RemoveWhere(item => item == null);
        while (pool.All.Count < Mathf.Max(0, count))
            pool.Idle.Push(Create(pool));
    }

    public Poolable Pop(GameObject original, Transform parent = null)
    {
        return Spawn(original, parent, false, Vector3.zero, Quaternion.identity);
    }

    public Poolable Pop(GameObject original, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        return Spawn(original, parent, true, position, rotation);
    }

    private Poolable Spawn(GameObject original, Transform parent, bool worldPose, Vector3 position, Quaternion rotation)
    {
        if (original == null || _clearing)
            return null;

        Pool pool = GetOrCreate(original);
        Poolable item = null;
        while (pool.Idle.Count > 0 && item == null)
            item = pool.Idle.Pop();
        if (item == null)
            item = Create(pool);

        item.transform.SetParent(parent, false);
        if (parent == null)
            SceneManager.MoveGameObjectToScene(item.gameObject, SceneManager.GetActiveScene());

        ResetTransform(item.transform, original.transform);
        if (worldPose)
            item.transform.SetPositionAndRotation(position, rotation);
        item.IsUsing = true;
        item.gameObject.SetActive(true);
        return item;
    }

    public bool Push(Poolable item)
    {
        if (item == null || _clearing || item.Owner != this || !item.IsUsing)
            return false;
        if (item.Original == null || !_pools.TryGetValue(item.Original, out Pool pool))
            return false;

        item.IsUsing = false;
        item.gameObject.SetActive(false);
        if (item == null || item.Owner != this || !_pools.TryGetValue(item.Original, out Pool current) || current != pool)
            return false;
        item.transform.SetParent(pool.Root, false);
        pool.Idle.Push(item);
        return true;
    }

    private Pool GetOrCreate(GameObject original)
    {
        if (_pools.TryGetValue(original, out Pool existing))
            return existing;
        if (_root == null)
        {
            _root = new GameObject("@Pool").transform;
            _root.SetParent(_host, false);
            _root.gameObject.SetActive(false);
        }

        Pool pool = new Pool { Original = original, Root = new GameObject(original.name + "_Pool").transform };
        pool.Root.SetParent(_root, false);
        _pools.Add(original, pool);
        return pool;
    }

    private Poolable Create(Pool pool)
    {
        GameObject clone = Object.Instantiate(pool.Original, pool.Root, false);
        clone.name = pool.Original.name;
        clone.SetActive(false);
        Poolable item = clone.GetComponent<Poolable>();
        if (item == null)
            item = clone.AddComponent<Poolable>();
        item.Owner = this;
        item.Original = pool.Original;
        item.IsUsing = false;
        pool.All.Add(item);
        return item;
    }

    private static void ResetTransform(Transform target, Transform original)
    {
        if (target is RectTransform rect && original is RectTransform originalRect)
        {
            rect.anchorMin = originalRect.anchorMin;
            rect.anchorMax = originalRect.anchorMax;
            rect.pivot = originalRect.pivot;
            rect.sizeDelta = originalRect.sizeDelta;
            rect.anchoredPosition3D = originalRect.anchoredPosition3D;
        }
        else
        {
            target.localPosition = original.localPosition;
        }
        target.localRotation = original.localRotation;
        target.localScale = original.localScale;
    }

    public void Clear()
    {
        if (_clearing)
            return;
        _clearing = true;
        try
        {
            List<Pool> pools = new List<Pool>(_pools.Values);
            _pools.Clear();
            foreach (Pool pool in pools)
            {
                foreach (Poolable item in pool.All)
                {
                    if (item == null)
                        continue;
                    item.Owner = null;
                    item.IsUsing = false;
                    item.gameObject.SetActive(false);
                    Object.Destroy(item.gameObject);
                }
                pool.All.Clear();
                pool.Idle.Clear();
                if (pool.Root != null)
                    Object.Destroy(pool.Root.gameObject);
            }
        }
        finally
        {
            _clearing = false;
        }
    }
}