using UnityEngine;

/// <summary>엔딩 복귀 요청이 있을 때만 메뉴 화면의 검은 오버레이를 서서히 제거한다.</summary>
[DisallowMultipleComponent]
public sealed class IntroFadeInController : MonoBehaviour
{
    private static bool _pending;

    [Header("메뉴 페이드인")]
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private CanvasGroup _menu;
    [SerializeField, Min(0.01f)] private float _duration = 2f;

    private float _elapsed;
    private bool _fading;
    private bool _menuWasInteractable;

    /// <summary>새 실행 세션에서 이전 실행의 페이드 요청을 비운다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRequest()
    {
        _pending = false;
    }

    /// <summary>다음 IntroScene 진입 시 검은 화면에서 페이드인하도록 요청한다.</summary>
    public static void RequestFadeIn()
    {
        _pending = true;
    }

    void Awake()
    {
        bool requested = _pending;
        _pending = false;
        if (_overlay == null || _menu == null)
        {
            Debug.LogError("메뉴 페이드 Overlay와 Menu 참조를 연결하세요.", this);
            enabled = false;
            return;
        }
        _overlay.alpha = requested ? 1f : 0f;
        _overlay.blocksRaycasts = requested;
        _overlay.interactable = false;
        _fading = requested;
        _menuWasInteractable = _menu.interactable;
        if (_fading) _menu.interactable = false;
    }

    void Update()
    {
        if (!_fading) return;
        _elapsed += Time.unscaledDeltaTime;
        _overlay.alpha = 1f - Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _duration));
        if (_overlay.alpha > 0f) return;
        _overlay.blocksRaycasts = false;
        _menu.interactable = _menuWasInteractable;
        _fading = false;
    }

    void OnDisable()
    {
        if (!_fading) return;
        if (_menu != null) _menu.interactable = _menuWasInteractable;
        _fading = false;
    }
}
