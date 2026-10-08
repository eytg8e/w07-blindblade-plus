using UnityEngine;

/// <summary>
/// 사건 한 종류의 관측 거리 정책과 독립 효과 프리팹 및 재생 수치를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "SensoryPresentationSettings", menuName = "Game/Sensory/Presentation Settings")]
public sealed class SensoryPresentationSettings : ScriptableObject
{
    [Header("사건 종류")]
    [SerializeField] private SensoryEventType _eventType;
    public SensoryEventType EventType => _eventType;

    [Header("관측 거리")]
    [SerializeField, Min(0f), Tooltip("효과를 완전히 표시하는 거리(미터)")]
    private float _fullVisibilityDistance = 5f;
    [SerializeField, Min(0.01f), Tooltip("모든 시각 효과를 생성하지 않는 거리(미터), 완전 표시 거리보다 커야 함")]
    private float _maximumDistance = 15f;
    [SerializeField, Tooltip("X: 완전 표시 거리부터 최대 거리까지 정규화한 거리, Y: 0~1 가시성")]
    private AnimationCurve _distanceVisibility = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public float FullVisibilityDistance => _fullVisibilityDistance;
    public float MaximumDistance => _maximumDistance;
    public AnimationCurve DistanceVisibility => _distanceVisibility;

    [Header("발자국")]
    [SerializeField, Tooltip("선택적 발자국 프리팹, 걸음 사건에서만 사용")]
    private FootstepObject _footstepPrefab;
    [SerializeField, Min(0.01f), Tooltip("발자국 표시 수명(초)")]
    private float _footstepLifetime = 3f;
    [SerializeField, Min(0.01f), Tooltip("발자국 수명 종료 직전 페이드 시간(초), 전체 수명 이내")]
    private float _footstepFadeDuration = 1f;
    [SerializeField, Min(0f), Tooltip("지면 법선 방향의 발자국 배치 간격(미터)")]
    private float _footstepSurfaceOffset = 0.01f;
    public FootstepObject FootstepPrefab => _eventType == SensoryEventType.Footstep ? _footstepPrefab : null;
    public float FootstepLifetime => _footstepLifetime;
    public float FootstepFadeDuration => _footstepFadeDuration;
    public float FootstepSurfaceOffset => _footstepSurfaceOffset;

    [Header("웨이브")]
    [SerializeField, Tooltip("선택적 독립 웨이브 프리팹")]
    private FootstepWave _wavePrefab;
    [SerializeField, Min(0.01f), Tooltip("웨이브 확산 및 표시 수명(초)")]
    private float _waveDuration = 0.6f;
    [SerializeField, Min(0f), Tooltip("웨이브 시작 반경(미터)")]
    private float _waveStartRadius = 0.05f;
    [SerializeField, Min(0f), Tooltip("웨이브 종료 반경(미터), 시작 반경 이상")]
    private float _waveEndRadius = 1.5f;
    public FootstepWave WavePrefab => _wavePrefab;
    public float WaveDuration => _waveDuration;
    public float WaveStartRadius => _waveStartRadius;
    public float WaveEndRadius => _waveEndRadius;

    [Header("순간 빛")]
    [SerializeField, Tooltip("선택적 독립 빛 효과 프리팹, 루트에 SensoryLight 필요")]
    private GameObject _lightPrefab;
    [SerializeField, ColorUsage(false), Tooltip("순간 빛 색상")]
    private Color _lightColor = Color.white;
    [SerializeField, Min(0f), Tooltip("프로젝트 렌더 파이프라인의 Light 강도 단위, 사건 강도 및 거리 가시성을 곱할 기준값")]
    private float _lightIntensity = 1f;
    [SerializeField, Min(0.01f), Tooltip("순간 빛 영향 반경(미터)")]
    private float _lightRange = 3f;
    [SerializeField, Min(0.01f), Tooltip("순간 빛 전체 표시 수명(초)")]
    private float _lightLifetime = 0.3f;
    [SerializeField, Min(0.01f), Tooltip("순간 빛 수명 종료 직전 페이드 시간(초), 전체 수명 이내")]
    private float _lightFadeDuration = 0.2f;
    [SerializeField, Min(0f), Tooltip("지면 법선 방향의 빛 배치 간격(미터)")]
    private float _lightSurfaceOffset = 0.25f;
    public float LightSurfaceOffset => _lightSurfaceOffset;
    public GameObject LightPrefab => _lightPrefab;
    public Color LightColor => _lightColor;
    public float LightIntensity => _lightIntensity;
    public float LightRange => _lightRange;
    public float LightLifetime => _lightLifetime;
    public float LightFadeDuration => _lightFadeDuration;

    /// <summary>
    /// 종류별 거리 정책과 사용하는 효과의 재생 수치가 허용 범위인지 반환한다.
    /// </summary>
    public bool IsValid()
    {
        if ((_eventType != SensoryEventType.Footstep
                && _eventType != SensoryEventType.Collision
                && _eventType != SensoryEventType.Sound)
            || !IsFinite(_fullVisibilityDistance) || !IsFinite(_maximumDistance)
            || _fullVisibilityDistance < 0f || _maximumDistance <= _fullVisibilityDistance
            || _distanceVisibility == null || _distanceVisibility.length < 2)
            return false;
        if (FootstepPrefab != null
            && (!IsFinite(_footstepLifetime) || !IsFinite(_footstepFadeDuration) || !IsFinite(_footstepSurfaceOffset)
                || _footstepLifetime <= 0f || _footstepFadeDuration <= 0f
                || _footstepFadeDuration > _footstepLifetime || _footstepSurfaceOffset < 0f))
            return false;
        if (_wavePrefab != null
            && (!IsFinite(_waveDuration) || !IsFinite(_waveStartRadius) || !IsFinite(_waveEndRadius)
                || _waveDuration <= 0f || _waveStartRadius < 0f || _waveEndRadius < _waveStartRadius))
            return false;
        if (_lightPrefab != null
            && (!IsFinite(_lightIntensity) || !IsFinite(_lightRange) || !IsFinite(_lightLifetime)
                || !IsFinite(_lightFadeDuration) || !IsFinite(_lightSurfaceOffset)
                || !IsFinite(_lightColor.r) || !IsFinite(_lightColor.g) || !IsFinite(_lightColor.b)
                || _lightSurfaceOffset < 0f || _lightIntensity < 0f || _lightRange <= 0f || _lightLifetime <= 0f
                || _lightFadeDuration <= 0f || _lightFadeDuration > _lightLifetime))
            return false;
        return true;
    }

    /// <summary>
    /// 수치가 유한한 값인지 검사하여 잘못된 외부 설정을 거부한다.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    void OnValidate()
    {
        if (!IsValid())
            Debug.LogError("감각 표현 설정의 사건 종류, 거리, 곡선 또는 효과 재생 수치를 확인해야 합니다.", this);
    }
}
