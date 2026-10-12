using UnityEngine;

using UnityEngine.UI;

/// <summary>플레이어 대시·회피 상태를 화면 가장자리 집중선으로 표시한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor), typeof(PlayerController))]
public sealed class PlayerDashVignette : MonoBehaviour
{
    [Header("대시 집중선")]
    [SerializeField] private Color _lineColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField, Range(16, 128)] private int _lineCount = 64;
    [SerializeField, Range(0.2f, 0.9f), Tooltip("집중선이 시작되는 화면 중심으로부터의 비율")] private float _innerRadius = 0.85f;
    [SerializeField, Min(0.01f), Tooltip("전체 비네팅이 나타나는 시간(초)")] private float _fadeInDuration = 0.06f;
    [SerializeField, Min(0.01f), Tooltip("전체 비네팅이 사라지는 시간(초)")] private float _fadeOutDuration = 0.15f;
    private PlayerMotor _motor;
    private PlayerController _player;
    private GameObject _canvasObject;
    private Image _image;
    private Material _material;
    private float _intensity;

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _player = GetComponent<PlayerController>();
        Shader shader = Resources.Load<Shader>("Effects/DashVignette");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("대시 집중선 셰이더가 없거나 지원되지 않습니다.", this);
            enabled = false;
            return;
        }
        _material = new Material(shader);
        _material.SetColor("_Color", _lineColor);
        _material.SetFloat("_LineCount", _lineCount);
        _material.SetFloat("_InnerRadius", _innerRadius);
        _canvasObject = new GameObject("DashVignetteCanvas", typeof(RectTransform), typeof(Canvas));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        GameObject imageObject = new GameObject("DashSpeedLines", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(_canvasObject.transform, false);
        _image = imageObject.GetComponent<Image>();
        _image.material = _material;
        _image.raycastTarget = false;
        RectTransform rect = _image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _image.enabled = false;
    }

    void OnEnable()
    {
        _intensity = 0f;
        _canvasObject.SetActive(true);
    }

    void LateUpdate()
    {
        bool blocked = _player.CurrentState == PlayerActionState.Dead
            || _player.CurrentState == PlayerActionState.Paused;
        bool moving = !blocked && (_motor.IsDashing || _motor.IsDodging);
        float duration = moving ? _fadeInDuration : _fadeOutDuration;
        _intensity = blocked ? 0f : Mathf.MoveTowards(
            _intensity, moving ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        _image.enabled = _intensity > 0f;
        _material.SetFloat("_Intensity", _intensity);
    }

    void OnDisable()
    {
        _intensity = 0f;
        if (_canvasObject != null)
        {
            _canvasObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
