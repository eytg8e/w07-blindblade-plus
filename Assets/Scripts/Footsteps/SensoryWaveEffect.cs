using UnityEngine;


[DisallowMultipleComponent]
[RequireComponent(typeof(Poolable))]
public sealed class FootstepWave : SensoryEffect
{
    [Header("파동 표시")]
    [SerializeField] private SpriteRenderer _ringRenderer;
    [SerializeField, Tooltip("기본 파동 링 색상")]
    private Color _ringColor = Color.white;
    [SerializeField, Min(0.01f), Tooltip("확산 및 표시 수명(초)")]
    private float _duration = 1.4f;
    [SerializeField, Min(0f), Tooltip("시작 반경(미터), 원본 링 지름은 1미터 기준")]
    private float _startRadius = 0.05f;
    [SerializeField, Min(0f), Tooltip("종료 반경(미터)")]
    private float _endRadius = 1.5f;
    private bool _isConfigured;

    void Awake()
    {
        _isConfigured = _ringRenderer != null;
        if (!_isConfigured)
        {
            Debug.LogError("파동 프리팹에 링 SpriteRenderer를 연결해야 합니다.", this);
            return;
        }
        _ringRenderer.color = _ringColor;
        _ringRenderer.enabled = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_isConfigured)
            _ringRenderer.enabled = false;
    }

    /// <summary>
    /// 공통 가시성과 인스펙터의 기본 수명·반경으로 파동을 독립 재생한다.
    /// </summary>
    public void Play(float visibility = 1f)
    {
        if (!_isConfigured)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        _ringRenderer.enabled = true;
        BeginPlayback(_duration, visibility);
    }

    /// <summary>
    /// 정규화 진행률과 외부 가시성으로 링 반경과 페이드 알파를 갱신한다.
    /// </summary>
    protected override void ApplyFrame(float elapsed, float progress)
    {
        float expansion = Mathf.SmoothStep(0f, 1f, progress);
        _ringRenderer.transform.localScale = Vector3.one
            * (Mathf.Lerp(_startRadius, _endRadius, expansion) * 2f);
        Color color = _ringColor;
        color.a *= (1f - Mathf.SmoothStep(0f, 1f, progress)) * Visibility;
        _ringRenderer.color = color;
    }
}
