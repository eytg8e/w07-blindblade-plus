using UnityEngine;

using UnityEngine.UI;

/// <summary>플레이어 HP 80% 이하에서 체력에 따른 붉은 비네팅을 표시한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerLowHealthVignette : MonoBehaviour
{
    private const float LOW_HEALTH_RATIO = 0.8f;
    private PlayerHealth _health;
    private GameObject _canvasObject;

    [Header("저체력 비네팅")]
    [SerializeField] private Color _vignetteColor = new Color(0.85f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("HP 80%에서의 불투명도")] private float _minVignetteOpacity = 0.08f;
    [SerializeField, Range(0f, 1f), Tooltip("HP 0%에서의 불투명도")] private float _maxVignetteOpacity = 0.7f;
    [SerializeField, Range(0.01f, 1f), Tooltip("HP 80%에서 화면 가장자리 두께 비율")] private float _minVignetteWidth = 0.15f;
    [SerializeField, Range(0.01f, 1f), Tooltip("HP 0%에서 화면 가장자리 두께 비율")] private float _maxVignetteWidth = 0.6f;
    [SerializeField, Range(0f, 0.1f), Tooltip("가장자리 일렁임의 두께 변화 진폭")] private float _rippleStrength = 0.02f;
    [SerializeField, Min(0f), Tooltip("일렁임 속도(초당 주기)")] private float _rippleSpeed = 0.8f;
    private Image _vignette;
    private Material _vignetteMaterial;

    void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _canvasObject = new GameObject("LowHealthVignetteCanvas", typeof(RectTransform), typeof(Canvas));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        CreateVignette();
    }

    void OnEnable()
    {
        _health.HealthChanged += HandleHealthChanged;
        _canvasObject.SetActive(true);
        HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
    }

    void Start()
    {
        HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
    }

    void OnDisable()
    {
        _health.HealthChanged -= HandleHealthChanged;
        _canvasObject.SetActive(false);
    }

    /// <summary>current와 maximum에서 체력 비율을 계산해 비네팅 표시를 갱신한다.</summary>
    private void HandleHealthChanged(float current, float maximum)
    {
        UpdateVignette(Mathf.Clamp01(current / maximum));
    }

    /// <summary>입력을 가로채지 않는 전체 화면 이미지와 전용 비네팅 머티리얼을 생성한다.</summary>
    private void CreateVignette()
    {
        Shader shader = Resources.Load<Shader>("Effects/LowHealthVignette");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("저체력 비네팅 셰이더가 없거나 지원되지 않습니다.", this);
            return;
        }
        _vignetteMaterial = new Material(shader);
        GameObject imageObject = new GameObject("LowHealthVignette", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(_canvasObject.transform, false);
        _vignette = imageObject.GetComponent<Image>();
        _vignette.color = Color.white;
        _vignette.raycastTarget = false;
        RectTransform rect = _vignette.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _vignette.material = _vignetteMaterial;
        _vignette.enabled = false;
    }

    /// <summary>ratio가 80% 이하이면 비네팅을 표시하고 체력이 낮을수록 불투명도와 두께를 증가시킨다.</summary>
    private void UpdateVignette(float ratio)
    {
        if (_vignette == null)
        {
            return;
        }
        _vignette.enabled = ratio <= LOW_HEALTH_RATIO;
        if (!_vignette.enabled)
        {
            return;
        }
        float severity = 1f - ratio / LOW_HEALTH_RATIO;
        _vignetteMaterial.SetColor("_Color", _vignetteColor);
        _vignetteMaterial.SetFloat("_RippleStrength", _rippleStrength);
        _vignetteMaterial.SetFloat("_RippleSpeed", _rippleSpeed);
        _vignetteMaterial.SetFloat("_Opacity",
            Mathf.Lerp(_minVignetteOpacity, Mathf.Max(_minVignetteOpacity, _maxVignetteOpacity), severity));
        _vignetteMaterial.SetFloat("_Width",
            Mathf.Lerp(_minVignetteWidth, Mathf.Max(_minVignetteWidth, _maxVignetteWidth), severity));
    }

    void OnDestroy()
    {
        if (_vignetteMaterial != null)
        {
            Destroy(_vignetteMaterial);
        }
    }
}
