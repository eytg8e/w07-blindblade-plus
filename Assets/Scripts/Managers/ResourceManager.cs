using System.Collections;
using UnityEngine;

public class ResourceManager
{
    private readonly PoolManager _pool;
    internal ResourceManager(PoolManager pool) { _pool = pool; }

    public T Load<T>(string path) where T : Object
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return Resources.Load<T>(path.Replace('\\', '/'));
    }

    public GameObject Instantiate(string path, Transform parent = null, bool usePool = true)
    {
        return Instantiate(LoadPrefab(path), parent, usePool);
    }

    public GameObject Instantiate(string path, Vector3 position, Quaternion rotation, Transform parent = null, bool usePool = true)
    {
        return Instantiate(LoadPrefab(path), position, rotation, parent, usePool);
    }

    public GameObject Instantiate(GameObject original, Transform parent = null, bool usePool = true)
    {
        if (original == null)
            return null;
        if (usePool && original.GetComponent<Poolable>() != null)
        {
            Poolable item = _pool.Pop(original, parent);
            return item != null ? item.gameObject : null;
        }
        GameObject clone = Object.Instantiate(original, parent, false);
        clone.name = original.name;
        return clone;
    }

    public GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent = null, bool usePool = true)
    {
        if (original == null)
            return null;
        if (usePool && original.GetComponent<Poolable>() != null)
        {
            Poolable item = _pool.Pop(original, position, rotation, parent);
            return item != null ? item.gameObject : null;
        }
        GameObject clone = Object.Instantiate(original, position, rotation, parent);
        clone.name = original.name;
        return clone;
    }

    public void Destroy(GameObject instance)
    {
        if (instance == null)
            return;
        Poolable item = instance.GetComponent<Poolable>();
        if (item != null && item.Owner == _pool)
        {
            _pool.Push(item);
            return;
        }
        instance.SetActive(false);
        Object.Destroy(instance);
    }

    private GameObject LoadPrefab(string path)
    {
        string fullPath = WithPrefix(path, "Prefabs/");
        GameObject original = Load<GameObject>(fullPath);
        if (original == null)
            Debug.LogError("Prefab not found in Resources: " + fullPath);
        return original;
    }

    internal static string WithPrefix(string path, string prefix)
    {
        if (string.IsNullOrWhiteSpace(path))
            return prefix;
        path = path.Replace('\\', '/').TrimStart('/');
        return path.StartsWith(prefix, System.StringComparison.Ordinal) ? path : prefix + path;
    }
}