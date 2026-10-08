using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 걸음 정보를 받아 관측 거리에 맞는 독립 시각 효과를 생성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SensoryManager : MonoBehaviour
{
    [Header("세션 연결")]
    [SerializeField] private Transform _observer;
    [SerializeField] private SensoryPresentationSettings _settings;
    private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>(64);
    private bool _initialized;
    private bool _reportedUnavailable;
    public bool IsReady => _initialized && _observer != null && isActiveAndEnabled;
    public Transform Observer => _observer;
    public int ActiveEffectCount => _activeEffects.Count;

    private readonly struct ActiveEffect
    {
        public readonly SensoryEffect Effect;
        public readonly int Version;
        public readonly Vector3 Position;

        /// <summary>
        /// 활성 효과와 사건 위치 및 재생 버전을 보관한다.
        /// </summary>
        public ActiveEffect(SensoryEffect effect, Vector3 position)
        {
            Effect = effect;
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
            float visibility = _observer != null
                ? CalculateVisibility(Vector3.Distance(entry.Position, _observer.position))
                : 0f;
            entry.Effect.SetVisibility(visibility);
        }
    }

    /// <summary>
    /// 표시 설정·프리팹 연결을 확인하고 세션에서 사용할 기존 풀을 준비한다.
    /// </summary>
    public bool Initialize()
    {
        _initialized = false;
        if (_settings == null)
        {
            Debug.LogError("SensoryManager 프리팹에 표시 설정을 연결해야 합니다.", this);
            return false;
        }
        if (_settings.FootstepPrefab != null)
            Managers.Pool.CreatePool(_settings.FootstepPrefab.gameObject);
        if (_settings.WavePrefab != null)
            Managers.Pool.CreatePool(_settings.WavePrefab.gameObject);
        if (_settings.LightPrefab != null)
            Managers.Pool.CreatePool(_settings.LightPrefab);
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
        SetObserver(null);
    }

    /// <summary>
    /// 걸음 위치와 관측자 거리를 판정하고 거리 안에서 독립 효과를 생성했는지 반환한다.
    /// </summary>
    public bool Emit(SensoryEvent sensoryEvent)
    {
        if (!IsReady)
        {
            if (!_reportedUnavailable)
            {
                Debug.LogError("SensoryManager의 세션 설정 또는 관측자가 준비되지 않아 요청을 거부합니다.", this);
                _reportedUnavailable = true;
            }
            return false;
        }
        float visibility = CalculateVisibility(Vector3.Distance(sensoryEvent.Position, _observer.position));
        if (visibility <= 0f)
            return false;
        Quaternion rotation = Quaternion.LookRotation(sensoryEvent.Direction, sensoryEvent.SurfaceNormal);
        bool spawned = false;
        if (_settings.FootstepPrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(_settings.FootstepPrefab.gameObject,
                sensoryEvent.Position + rotation * _settings.FootstepPrefab.transform.localPosition, rotation);
            if (instance != null)
            {
                FootstepObject effect = instance.GetComponent<FootstepObject>();
                effect.Play(sensoryEvent.IsLeft, visibility);
                spawned |= Track(effect, sensoryEvent.Position);
            }
        }
        if (_settings.WavePrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(_settings.WavePrefab.gameObject,
                sensoryEvent.Position + rotation * _settings.WavePrefab.transform.localPosition, rotation);
            if (instance != null)
            {
                FootstepWave effect = instance.GetComponent<FootstepWave>();
                effect.Play(visibility);
                spawned |= Track(effect, sensoryEvent.Position);
            }
        }
        if (_settings.LightPrefab != null)
        {
            GameObject instance = Managers.Resource.Instantiate(_settings.LightPrefab,
                sensoryEvent.Position + rotation * _settings.LightPrefab.transform.localPosition, rotation);
            if (instance != null)
            {
                SensoryLight effect = instance.GetComponent<SensoryLight>();
                effect.Play(visibility);
                spawned |= Track(effect, sensoryEvent.Position);
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
    /// 재생 중인 효과와 위치를 등록하고 생성 성공 여부를 반환한다.
    /// </summary>
    private bool Track(SensoryEffect effect, Vector3 position)
    {
        if (!effect.IsPlaying)
            return false;
        _activeEffects.Add(new ActiveEffect(effect, position));
        return true;
    }

    /// <summary>
    /// 거리로 선형 감쇠한 가시성을 반환하며 최대 거리 이상에서는 0을 반환한다.
    /// </summary>
    private float CalculateVisibility(float distance)
    {
        if (distance >= _settings.MaximumDistance)
            return 0f;
        if (distance <= _settings.FullVisibilityDistance)
            return 1f;
        float progress = Mathf.InverseLerp(_settings.FullVisibilityDistance, _settings.MaximumDistance, distance);
        return 1f - progress;
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
