using UnityEngine;

using UnityEngine.UI;

/// <summary>보스 체력 사건을 화면 상단 체력바에 연결하고 보스와 함께 표시 수명을 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class BossHealthBar : MonoBehaviour
{
    [Header("체력바 표시")]
    [SerializeField] private Vector2 _size = new Vector2(420f, 16f);
    [SerializeField, Min(0f)] private float _topOffset = 36f;
    private EnemyHealth _health;
    private GameObject _canvasObject;
    private Image _fill;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _canvasObject = new GameObject("BossHealthCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        Image background = CreateImage("Background", _canvasObject.transform, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -_topOffset);
        rect.sizeDelta = _size;
        _fill = CreateImage("Health", rect, Color.white);
        _fill.rectTransform.anchorMin = Vector2.zero;
        _fill.rectTransform.anchorMax = Vector2.one;
        _fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        _fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
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

    /// <summary>name과 color로 입력을 가로채지 않는 UI 이미지를 parent 아래 생성해 반환한다.</summary>
    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform), typeof(Image));
        instance.transform.SetParent(parent, false);
        Image image = instance.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>current와 maximum의 체력 비율로 체력바 오른쪽 경계를 갱신한다.</summary>
    private void HandleHealthChanged(float current, float maximum)
    {
        float ratio = Mathf.Clamp01(current / maximum);
        _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        _fill.enabled = ratio > 0f;
    }
}
