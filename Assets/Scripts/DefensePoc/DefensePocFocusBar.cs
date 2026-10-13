using UnityEngine;

using UnityEngine.UI;

/// <summary>
/// DefenseTest 집중 게이지의 남은 비율을 화면 하단에 표시한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DefensePocPlayer))]
public sealed class DefensePocFocusBar : MonoBehaviour
{
    [Header("집중 게이지 표시")]
    [SerializeField] private Vector2 _size = new Vector2(280f, 14f);
    [SerializeField, Min(0f)] private float _bottomOffset = 84f;
    private DefensePocPlayer _focus;
    private GameObject _canvasObject;
    private Image _fill;

    void Awake()
    {
        _focus = GetComponent<DefensePocPlayer>();
        _canvasObject = new GameObject("DefensePocFocusCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        Image background = CreateImage("Background", _canvasObject.transform,
            new Color(0.1f, 0.15f, 0.2f, 0.9f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, _bottomOffset);
        rect.sizeDelta = _size;
        _fill = CreateImage("Focus", rect, new Color(0.45f, 0.85f, 1f));
        _fill.rectTransform.anchorMin = Vector2.zero;
        _fill.rectTransform.anchorMax = Vector2.one;
        _fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        _fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
    }

    void OnEnable()
    {
        if (_canvasObject != null) _canvasObject.SetActive(true);
    }

    void OnDisable()
    {
        if (_canvasObject != null) _canvasObject.SetActive(false);
    }

    void Update()
    {
        float ratio = Mathf.Clamp01(_focus.FocusFraction);
        _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        _fill.enabled = ratio > 0f;
    }

    /// <summary>
    /// name과 color로 입력을 가로채지 않는 이미지를 parent에 만들고 반환한다.
    /// </summary>
    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform), typeof(Image));
        instance.transform.SetParent(parent, false);
        Image image = instance.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
