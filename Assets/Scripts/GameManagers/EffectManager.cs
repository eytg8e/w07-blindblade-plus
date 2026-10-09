using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using ReusableManagers;

/// <summary>
/// 세션의 공통 효과 재생 요청을 처리하고 완료된 객체를 기존 풀로 반환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EffectManager : MonoBehaviour
{
    [Header("효과 설정")]
    [SerializeField] private EffectLibrary _library;

    [Header("활성 효과 배치")]
    private Transform _activeRoot;

    [Header("세션 상태")]
    private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>(32);
    private Scene _sessionScene;
    private bool _initialized;
    private bool _sessionActive;
    private bool _reportedNotReady;
    private int _nextTrailHandle = 1;
    public bool IsReady => _initialized && _sessionActive && isActiveAndEnabled;
    public int ActiveEffectCount => _activeEffects.Count;

    private readonly struct ActiveEffect
    {
        public Poolable Pooled { get; }
        public ParticleSystem Particles { get; }
        public SwordSlashTrail Trail { get; }
        public int Handle { get; }
        public bool IsPlaying => Particles != null ? Particles.IsAlive(true) : Trail != null && Trail.IsPlaying;

        /// <summary>
        /// 풀 객체·단발 또는 궤적 컴포넌트·요청 번호로 활성 효과를 등록한다.
        /// 주어진 참조와 번호를 재생 완료·반환 처리에 사용할 상태로 보관한다.
        /// </summary>
        public ActiveEffect(Poolable pooled, ParticleSystem particles, SwordSlashTrail trail, int handle)
        {
            Pooled = pooled;
            Particles = particles;
            Trail = trail;
            Handle = handle;
        }
    }

    void Awake()
    {
        _activeRoot = new GameObject("@ActiveEffects").transform;
        DontDestroyOnLoad(_activeRoot.gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EndSession();
    }

    void OnDestroy()
    {
        if (_activeRoot != null)
            Destroy(_activeRoot.gameObject);
    }

    void LateUpdate()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _activeEffects[i];
            if (effect.Pooled != null && effect.Pooled.IsUsing && effect.IsPlaying)
                continue;
            ReturnEffect(effect);
            _activeEffects.RemoveAt(i);
        }
    }

    /// <summary>
    /// 연결된 Library로 기존 풀을 준비하고 현재 활성 씬의 세션을 시작한다.
    /// 설정이 유효하면 재생 준비 상태를 만들고 true를 반환한다.
    /// </summary>
    public bool Initialize()
    {
        if (_initialized)
            return true;
        if (_library == null || !_library.PreparePools())
        {
            Debug.LogError("[EffectManager] A valid EffectLibrary is required.", this);
            return false;
        }
        _initialized = true;
        return BeginSession();
    }

    /// <summary>
    /// 현재 활성 씬을 새로운 효과 세션으로 등록한다.
    /// 이전 효과를 반환한 뒤 재생 요청을 허용하며 성공 여부를 반환한다.
    /// </summary>
    public bool BeginSession()
    {
        return BeginSession(SceneManager.GetActiveScene());
    }

    /// <summary>
    /// effectId에 등록된 단발 효과를 position과 rotation으로 풀에서 가져와 재생한다.
    /// 완료 추적 목록에 등록하고 재생 요청의 성공 여부를 반환한다.
    /// </summary>
    public bool Play(EffectId effectId, Vector3 position, Quaternion rotation)
    {
        if (!TryResolvePrefab(effectId, out GameObject prefab))
            return false;
        if (prefab.GetComponent<ParticleSystem>() == null)
        {
            Debug.LogWarning("[EffectManager] Use BeginTrail for sword trails.", this);
            return false;
        }
        Poolable pooled = Managers.Pool.Pop(prefab, position, rotation, _activeRoot);
        if (pooled == null)
            return false;
        ParticleSystem particles = pooled.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Play(true);
        _activeEffects.Add(new ActiveEffect(pooled, particles, null, 0));
        return true;
    }

    /// <summary>
    /// effectId와 월드 position으로 단발 파티클 프리팹을 기본 회전으로 재생한다.
    /// 접촉점만 필요한 효과의 생성·재생 성공 여부를 반환한다.
    /// </summary>
    public bool Play(EffectId effectId, Vector3 position)
    {
        return Play(effectId, position, Quaternion.identity);
    }

    /// <summary>
    /// 월드 position에 공용 원형 Hit 파티클을 재생한다.
    /// 접촉점만 받아 기존 SwordClash 등록으로 생성하며 성공 여부를 반환한다.
    /// </summary>
    public bool PlayHit(Vector3 position)
    {
        return Play(EffectId.SwordClash, position);
    }

    /// <summary>
    /// 실제 검 양 끝점으로 Library의 SwordSlashTrail을 풀에서 가져와 기록을 시작한다.
    /// 유효한 재생이면 종료 요청에 사용할 고유 번호, 실패하면 0을 반환한다.
    /// </summary>
    public int BeginTrail(Transform bladeBase, Transform bladeTip)
    {
        if (!TryResolvePrefab(EffectId.SwordSlashTrail, out GameObject prefab))
            return 0;
        if (bladeBase == null || bladeTip == null
            || !bladeBase.gameObject.activeInHierarchy || !bladeTip.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[EffectManager] BeginTrail requires active blade endpoints.", this);
            return 0;
        }
        Poolable pooled = Managers.Pool.Pop(prefab, Vector3.zero, Quaternion.identity, _activeRoot);
        if (pooled == null)
            return 0;
        SwordSlashTrail trail = pooled.GetComponent<SwordSlashTrail>();
        int handle = _nextTrailHandle++;
        trail.BeginTrail(bladeBase, bladeTip);
        _activeEffects.Add(new ActiveEffect(pooled, null, trail, handle));
        return handle;
    }

    /// <summary>
    /// trailHandle에 대응하는 현재 검 궤적의 기록을 중단한다.
    /// 남은 잔상은 자연 종료하도록 유지하며 유효한 번호이면 true를 반환한다.
    /// </summary>
    public bool EndTrail(int trailHandle)
    {
        foreach (ActiveEffect effect in _activeEffects)
        {
            if (trailHandle <= 0 || effect.Handle != trailHandle || effect.Trail == null)
                continue;
            effect.Trail.EndTrail();
            return true;
        }
        return false;
    }

    /// <summary>
    /// trailHandle에 대응하는 검 궤적의 잔상과 기록을 즉시 제거하고 풀로 반환한다.
    /// 취소에 성공하면 true, 이미 종료된 요청 번호이면 false를 반환한다.
    /// </summary>
    public bool StopTrail(int trailHandle)
    {
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            ActiveEffect effect = _activeEffects[i];
            if (trailHandle <= 0 || effect.Handle != trailHandle || effect.Trail == null)
                continue;
            ReturnEffect(effect);
            _activeEffects.RemoveAt(i);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 현재 세션의 모든 활성 효과를 풀로 반환하고 새 재생 요청을 차단한다.
    /// 다른 시스템의 풀은 유지하고 추적 목록 및 세션 씬만 초기화한다.
    /// </summary>
    public void EndSession()
    {
        _sessionActive = false;
        foreach (ActiveEffect effect in _activeEffects)
            ReturnEffect(effect);
        _activeEffects.Clear();
        _sessionScene = default;
    }

    /// <summary>
    /// effectId를 조회하기 전에 세션 준비 상태를 확인한다.
    /// 미준비 안내는 상태 구간마다 한 번 기록하고 유효한 prefab 조회 여부를 반환한다.
    /// </summary>
    private bool TryResolvePrefab(EffectId effectId, out GameObject prefab)
    {
        prefab = null;
        if (!IsReady)
        {
            if (!_reportedNotReady)
            {
                Debug.LogWarning("[EffectManager] Effect session is not ready. Initialize or BeginSession before playback.", this);
                _reportedNotReady = true;
            }
            return false;
        }
        return _library.TryGetPrefab(effectId, out prefab);
    }

    /// <summary>
    /// scene을 효과 세션으로 설정하고 이전 세션의 활성 객체를 반환한다.
    /// 초기화된 매니저와 로드된 씬일 때 세션을 시작하고 true를 반환한다.
    /// </summary>
    private bool BeginSession(Scene scene)
    {
        if (!_initialized || !isActiveAndEnabled || !scene.IsValid() || !scene.isLoaded)
            return false;
        EndSession();
        _reportedNotReady = false;
        _sessionScene = scene;
        _sessionActive = true;
        return true;
    }

    /// <summary>
    /// 활성 효과의 검 참조를 해제하고 소유 중인 객체를 기존 Resource/Pool로 반환한다.
    /// effect를 사용하며 공통 매니저 종료 중에는 객체를 직접 제거한다.
    /// </summary>
    private void ReturnEffect(ActiveEffect effect)
    {
        if (effect.Trail != null)
            effect.Trail.ReleaseBlade();
        if (effect.Particles != null)
            effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (effect.Pooled == null || !effect.Pooled.IsUsing)
            return;
        if (Managers.IsInitialized && !Game.IsQuitting)
            Managers.Resource.Destroy(effect.Pooled.gameObject);
        else
            Destroy(effect.Pooled.gameObject);
    }

    /// <summary>
    /// 언로드된 scene이 현재 세션 씬이면 활성 효과를 모두 반환한다.
    /// 관련 없는 Additive 씬의 언로드는 현재 세션을 유지한다.
    /// </summary>
    private void HandleSceneUnloaded(Scene scene)
    {
        if (_sessionActive && scene == _sessionScene)
            EndSession();
    }

    /// <summary>
    /// Single 방식으로 로드된 scene을 새 효과 세션으로 시작한다.
    /// 초기화된 매니저만 재시작하며 Additive 로드에서는 기존 세션을 유지한다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_initialized && mode == LoadSceneMode.Single)
            BeginSession(scene);
    }
}
