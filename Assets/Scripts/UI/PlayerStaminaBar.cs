using UnityEngine;

using UnityEngine.UI;

/// <summary>플레이어 스태미나 사건을 화면 하단 스태미나바에 연결하고 플레이어와 함께 표시 수명을 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStamina))]
public sealed class PlayerStaminaBar : MonoBehaviour
{
    [Header("스태미나바 표시")]
    [SerializeField] private Vector2 _size = new Vector2(280f, 14f);
    [SerializeField, Min(0f)] private float _bottomOffset = 60f;
    private PlayerStamina _stamina;
    private GameObject _canvasObject;
    private Image _fill;

    void Awake()
    {
        _stamina = GetComponent<PlayerStamina>();
        _canvasObject = new GameObject("PlayerStaminaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        Image background = CreateImage("Background", _canvasObject.transform, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, _bottomOffset);
        rect.sizeDelta = _size;
        _fill = CreateImage("Health", rect, new Color(0.3f, 0.8f, 0.5f));
        _fill.rectTransform.anchorMin = Vector2.zero;
        _fill.rectTransform.anchorMax = Vector2.one;
        _fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        _fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
    }

    void OnEnable()
    {
        _stamina.Changed += HandleStaminaChanged;
        _canvasObject.SetActive(true);
        HandleStaminaChanged(_stamina.Current, _stamina.Maximum);
    }

    void Start()
    {
        HandleStaminaChanged(_stamina.Current, _stamina.Maximum);
    }

    void OnDisable()
    {
        _stamina.Changed -= HandleStaminaChanged;
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

    /// <summary>current와 maximum의 스태미나 비율로 스태미나바 오른쪽 경계를 갱신한다.</summary>
    private void HandleStaminaChanged(float current, float maximum)
    {
        float ratio = Mathf.Clamp01(current / maximum);
        _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        _fill.enabled = ratio > 0f;
    }
}
