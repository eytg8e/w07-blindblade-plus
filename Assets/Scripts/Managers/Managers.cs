using ReusableManagers;
using System;
using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers _instance;
    private static bool _quitting;
    private bool _initialized;
    private bool _clearing;
    private ResourceManager _resource;
    private PoolManager _pool;
    private SoundManager _sound;
    private SceneManagerEx _scene;

    public static ResourceManager Resource => Instance._resource;
    public static PoolManager Pool => Instance._pool;
    public static SoundManager Sound => Instance._sound;
    public static SceneManagerEx Scene => Instance._scene;
    public static bool IsInitialized => _instance != null && _instance._initialized;
    public static bool IsQuitting => _quitting;

    public static Managers Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            if (_quitting || !Application.isPlaying)
                throw new InvalidOperationException("Managers can only be created during Play Mode.");

            foreach (Managers candidate in Resources.FindObjectsOfTypeAll<Managers>())
            {
                if (!candidate.gameObject.scene.IsValid())
                    continue;
                candidate.Initialize();
                return _instance;
            }

            new GameObject("@ReusableManagers").AddComponent<Managers>();
            return _instance;
        }
    }

    public static void Init()
    {
        Managers instance = Instance;
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        if (_initialized)
            return;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        _pool = new PoolManager(transform);
        _resource = new ResourceManager(_pool);
        _sound = new SoundManager(transform, _resource);
        _scene = new SceneManagerEx();
        _initialized = true;
    }

    public static void Clear()
    {
        if (IsInitialized)
            _instance.ClearServices();
    }

    private void ClearServices()
    {
        if (_clearing)
            return;
        _clearing = true;
        try
        {
            _sound.Clear();
            _pool.Clear();
        }
        finally
        {
            _clearing = false;
        }
    }

    private void OnApplicationQuit()
    {
        if (_instance == this)
            _quitting = true;
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        if (_initialized)
            ClearServices();
        _initialized = false;
        _instance = null;
    }
}
