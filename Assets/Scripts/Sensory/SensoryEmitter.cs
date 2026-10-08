using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 여러 발생원의 사건을 받아 관측 거리와 종류에 맞는 독립 시각 효과를 생성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SensoryEmitter : MonoBehaviour
{
    private const int EVENT_TYPE_COUNT = 3;

    [Header("세션 연결")]
    [SerializeField] private Transform _observer;
    [SerializeField, Tooltip("걸음·충돌·소리 종류마다 하나씩 연결하는 표시 정책")]
    private SensoryPresentationSettings[] _profiles;
    private readonly SensoryPresentationSettings[] _settings = new SensoryPresentationSettings[EVENT_TYPE_COUNT];
    private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>(64);
    private bool _initialized;
    private bool _reportedUnavailable;
    public bool IsReady => _initialized && _observer != null && isActiveAndEnabled;
    public Transform Observer => _observer;
    public int ActiveEffectCount => _activeEffects.Count;

    private readonly struct ActiveEffect
    {
        public readonly SensoryEffect Effect;
        public readonly SensoryPresentationSettings Settings;
        public readonly float Intensity;
        public readonly int Version;
        public readonly Vector3 Position;

        /// <summary>
        /// 활성 효과와 표시 설정 및 강도·재생 버전을 보관한다.
        /// </summary>
        public ActiveEffect(SensoryEffect effect, SensoryPresentationSettings settings, float intensity, Vector3 position)
        {
            Effect = effect;
            Settings = settings;
            Intensity = intensity;
            Version = effect.PlaybackVersion;
            Position = position;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        EndSession();
    }

    void LateUpdate()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect entry = _activeEffects[i];
            if (entry.Effect == null || !entry.Effect.IsPlaying
                || entry.Effect.PlaybackVersion != entry.Version)
            {
                _activeEffects.RemoveAt(i);
                continue;
            }
            float visibility = _observer != null && entry.Settings != null
                ? CalculateVisibility(Vector3.Distance(entry.Position, _observer.position), entry.Settings)
                : 0f;
            entry.Effect.SetVisibility(visibility * entry.Intensity);
        }
    }

    /// <summary>
    /// 세션의 필수 관측자와 사건별 정책·프리팹을 검증하고 기존 풀을 준비한다.
    /// </summary>
    public bool Initialize()
    {
        _initialized = false;
        if (_observer == null || _profiles == null || _profiles.Length != EVENT_TYPE_COUNT)
        {
            Debug.LogError("감각 Emitter에 관측자와 사건 종류별 설정 3개를 연결해야 합니다.", this);
            return false;
        }
        for (int i = 0; i < _settings.Length; i++)
            _settings[i] = null;
        foreach (SensoryPresentationSettings profile in _profiles)
        {
            if (profile == null || !profile.IsValid())
            {
                Debug.LogError("감각 표시 설정이 없거나 수치가 유효하지 않습니다.", this);
                return false;
            }
            int index = (int)profile.EventType;
            if (_settings[index] != null)
            {
                Debug.LogError("감각 표시 설정의 사건 종류가 중복되어 있습니다.", this);
                return false;
            }
            if (profile.LightPrefab != null && profile.LightPrefab.GetComponent<SensoryLight>() == null)
            {
                Debug.LogError("빛 프리팹 루트에 SensoryLight가 필요합니다.", profile.LightPrefab);
                return false;
            }
            _settings[index] = profile;
        }
        foreach (SensoryPresentationSettings profile in _settings)
        {
            if (profile.FootstepPrefab != null)
                Managers.Pool.CreatePool(profile.FootstepPrefab.gameObject);
            if (profile.WavePrefab != null)
                Managers.Pool.CreatePool(profile.WavePrefab.gameObject);
            if (profile.LightPrefab != null)
                Managers.Pool.CreatePool(profile.LightPrefab);
        }
        _initialized = true;
        _reportedUnavailable = false;
        return true;
    }

    /// <summary>
    /// 새 관측자 참조로 거리 기준을 교체하고 다음 요청의 준비 상태를 갱신한다.
    /// </summary>
    public void SetObserver(Transform observer)
    {
        _observer = observer;
        _reportedUnavailable = false;
        LateUpdate();
    }

    /// <summary>
    /// 관측자를 해제하고 남아 있는 효과의 표시를 즉시 숨긴다.
    /// </summary>
    public void ClearObserver()
    {
        _observer = null;
        foreach (ActiveEffect entry in _activeEffects)
        {
            if (entry.Effect != null && entry.Effect.IsPlaying && entry.Effect.PlaybackVersion == entry.Version)
                entry.Effect.SetVisibility(0f);
        }
    }

    /// <summary>
    /// 사건의 위치·종류·강도를 검증하고 거리 안에서 독립 효과를 생성했는지 반환한다.
    /// </summary>
    public bool Emit(SensoryEvent sensoryEvent)
    {
        if (!IsReady)
        {
            if (!_reportedUnavailable)
            {
                Debug.LogError("감각 Emitter의 세션 설정 또는 관측자가 준비되지 않아 요청을 거부합니다.", this);
                _reportedUnavailable = true;
            }
            return false;
        }
        int index = (int)sensoryEvent.Type;
        if (index < 0 || index >= EVENT_TYPE_COUNT || !IsFinite(sensoryEvent.Position)
            || !IsFinite(sensoryEvent.SurfaceNormal) || !IsFinite(sensoryEvent.Direction)
            || sensoryEvent.SurfaceNormal.sqrMagnitude < 0.001f || sensoryEvent.Direction.sqrMagnitude < 0.001f
            || float.IsNaN(sensoryEvent.Intensity) || float.IsInfinity(sensoryEvent.Intensity)
            || (sensoryEvent.Type == SensoryEventType.Footstep && sensoryEvent.Side == FootSide.None))
        {
            Debug.LogError("감각 사건의 위치·방향·종류·강도 또는 발 정보가 유효하지 않습니다.", this);
            return false;
        }
        SensoryPresentationSettings settings = _settings[index];
        if (settings == null || !settings.IsValid())
        {
            Debug.LogError("감각 사건의 표시 설정이 누락되었거나 유효하지 않습니다.", this);
            return false;
        }
        float visibility = CalculateVisibility(Vector3.Distance(sensoryEvent.Position, _observer.position), settings)
            * sensoryEvent.Intensity;
        if (visibility <= 0f)
            return false;
        Quaternion rotation = Quaternion.LookRotation(sensoryEvent.Direction, sensoryEvent.SurfaceNormal);
        bool spawned = false;
        if (settings.FootstepPrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(settings.FootstepPrefab.gameObject,
                sensoryEvent.Position + sensoryEvent.SurfaceNormal * settings.FootstepSurfaceOffset, rotation);
            if (instance != null)
            {
                FootstepObject effect = instance.GetComponent<FootstepObject>();
                effect.Play(sensoryEvent.Side == FootSide.Left, visibility,
                    settings.FootstepLifetime, settings.FootstepFadeDuration);
                spawned |= Track(effect, settings, sensoryEvent.Intensity, sensoryEvent.Position);
            }
        }
        if (settings.WavePrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(settings.WavePrefab.gameObject, sensoryEvent.Position, rotation);
            if (instance != null)
            {
                FootstepWave effect = instance.GetComponent<FootstepWave>();
                effect.Play(visibility, settings.WaveDuration, settings.WaveStartRadius, settings.WaveEndRadius);
                spawned |= Track(effect, settings, sensoryEvent.Intensity, sensoryEvent.Position);
            }
        }
        if (settings.LightPrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(settings.LightPrefab,
                sensoryEvent.Position + sensoryEvent.SurfaceNormal * settings.LightSurfaceOffset, rotation);
            if (instance != null)
            {
                SensoryLight effect = instance.GetComponent<SensoryLight>();
                effect.Play(visibility, settings.LightColor, settings.LightIntensity, settings.LightRange,
                    settings.LightLifetime, settings.LightFadeDuration);
                spawned |= Track(effect, settings, sensoryEvent.Intensity, sensoryEvent.Position);
            }
        }
        return spawned;
    }

    /// <summary>
    /// 세션의 유효한 재생 효과를 반환하고 관측자와 초기화 상태를 해제한다.
    /// </summary>
    public void EndSession()
    {
        foreach (ActiveEffect entry in _activeEffects)
        {
            if (entry.Effect == null || !entry.Effect.IsPlaying || entry.Effect.PlaybackVersion != entry.Version)
                continue;
            if (Managers.IsInitialized)
                Managers.Resource.Destroy(entry.Effect.gameObject);
            else
                Destroy(entry.Effect.gameObject);
        }
        _activeEffects.Clear();
        _observer = null;
        _initialized = false;
        _reportedUnavailable = false;
    }

    /// <summary>
    /// 재생 중인 효과와 표시 설정을 등록하고 생성 성공 여부를 반환한다.
    /// </summary>
    private bool Track(SensoryEffect effect, SensoryPresentationSettings settings, float intensity, Vector3 position)
    {
        if (!effect.IsPlaying)
            return false;
        _activeEffects.Add(new ActiveEffect(effect, settings, intensity, position));
        return true;
    }

    /// <summary>
    /// 거리와 종류별 곡선으로 가시성을 반환하며 최대 거리 이상에서는 0을 반환한다.
    /// </summary>
    private static float CalculateVisibility(float distance, SensoryPresentationSettings settings)
    {
        if (distance >= settings.MaximumDistance)
            return 0f;
        if (distance <= settings.FullVisibilityDistance)
            return 1f;
        float progress = Mathf.InverseLerp(settings.FullVisibilityDistance, settings.MaximumDistance, distance);
        float visibility = settings.DistanceVisibility.Evaluate(progress);
        return float.IsNaN(visibility) || float.IsInfinity(visibility) ? 0f : Mathf.Clamp01(visibility);
    }

    /// <summary>
    /// 벡터의 각 성분이 유한한지 검사하여 외부 사건 좌표의 유효성을 반환한다.
    /// </summary>
    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }

    /// <summary>
    /// 관측자 소유 씬이 언로드되면 이전 세션의 효과와 참조를 정리한다.
    /// </summary>
    private void HandleSceneUnloaded(Scene scene)
    {
        if (_initialized && (_observer == null || _observer.gameObject.scene == scene))
            EndSession();
    }
}
