using UnityEngine;

/// <summary>
/// 걸음 표현의 관측 거리와 독립 효과 프리팹을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "SensoryPresentationSettings", menuName = "Game/Sensory/Presentation Settings")]
public sealed class SensoryPresentationSettings : ScriptableObject
{
    [Header("관측 거리")]
    [SerializeField, Min(0f), Tooltip("효과를 완전히 표시하는 거리(미터)")]
    private float _fullVisibilityDistance = 5f;
    [SerializeField, Min(0.01f), Tooltip("효과를 숨기는 거리(미터), 완전 표시 거리보다 커야 함")]
    private float _maximumDistance = 15f;
    public float FullVisibilityDistance => _fullVisibilityDistance;
    public float MaximumDistance => _maximumDistance;

    [Header("독립 효과")]
    [SerializeField] private FootstepObject _footstepPrefab;
    [SerializeField] private FootstepWave _wavePrefab;
    [SerializeField] private GameObject _lightPrefab;
    public FootstepObject FootstepPrefab => _footstepPrefab;
    public FootstepWave WavePrefab => _wavePrefab;
    public GameObject LightPrefab => _lightPrefab;

    void OnValidate()
    {
        if (_maximumDistance <= _fullVisibilityDistance)
            Debug.LogError("Maximum Distance는 Full Visibility Distance보다 커야 합니다.", this);
    }
}
