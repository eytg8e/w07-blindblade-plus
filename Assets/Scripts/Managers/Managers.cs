using UnityEngine;

using ReusableManagers;

public class Managers : MonoSingleton<Managers>
{
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
    public new static bool IsInitialized => CurrentInstance != null && CurrentInstance._initialized;
    protected override string SingletonObjectName => "@ReusableManagers";
    protected override bool IncludeInactiveInstances => true;

    /// <summary>
    /// 싱글턴에 접근하여 매니저 서비스를 초기화한다.
    /// </summary>
    public static void Init()
    {
        Managers instance = Instance;
    }

    void Update()
    {
        _sound.Update();
    }

    /// <summary>
    /// 싱글턴을 등록하고 풀, 리소스, 사운드, 씬 서비스를 초기화한다.
    /// </summary>
    protected override void InitializeSingleton()
    {
        if (CurrentInstance != null && CurrentInstance != this)
        {
            base.InitializeSingleton();
            return;
        }

        transform.SetParent(null);
        base.InitializeSingleton();
        if (_initialized)
            return;

        _pool = new PoolManager(transform);
        _resource = new ResourceManager(_pool);
        _sound = new SoundManager(_resource.Load<SoundLibrary>("Sound/SoundLibrary"));
        _scene = new SceneManagerEx();
        _initialized = true;
    }

    /// <summary>
    /// 초기화된 싱글턴의 사운드와 풀 서비스를 정리한다.
    /// </summary>
    public static void Clear()
    {
        if (IsInitialized)
            CurrentInstance.ClearServices();
    }

    /// <summary>
    /// 중복 정리를 방지하며 사운드와 풀의 보유 자원을 해제한다.
    /// </summary>
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

    /// <summary>
    /// 중복 매니저 컴포넌트만 제거하여 같은 오브젝트의 다른 컴포넌트를 유지한다.
    /// </summary>
    protected override void DestroyDuplicate()
    {
        Destroy(this);
    }

    protected override void OnDestroy()
    {
        if (CurrentInstance != this)
            return;

        if (_initialized)
            ClearServices();
        _initialized = false;
        base.OnDestroy();
    }
}
