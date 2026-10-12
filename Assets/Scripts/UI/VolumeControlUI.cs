using System;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

/// <summary>음량 패널의 표시와 전체 음량의 복원·변경·저장을 담당한다.</summary>
public sealed class VolumeControlUI : MonoBehaviour
{
    private const string VOLUME_PREF_KEY = "IntroMenu.Volume";

    [Header("음량 UI")]
    [SerializeField] private Button _volumeButton;
    [SerializeField] private GameObject _volumePanel;
    [SerializeField] private Slider _volumeSlider;

    private GameObject _modalRoot;
    private GameObject _panelContainer;
    private Button _closeButton;
    private TMP_Text _buttonText;
    private EventSystem _eventSystem;
    private Button[] _menuButtons;
    private bool[] _menuButtonStates;
    private bool _menuInputBlocked;
    public event Action<Button, Action> OpenRequested;
    public event Action PanelClosed;

    void Awake()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_PREF_KEY, 1f));
        _volumeSlider.SetValueWithoutNotify(volume);
        ApplyVolume(volume);
        _buttonText = _volumeButton.GetComponentInChildren<TMP_Text>();
        _eventSystem = EventSystem.current;
        CreateModal();
        _menuButtons = _volumeButton.transform.parent.GetComponentsInChildren<Button>(true);
        _menuButtonStates = new bool[_menuButtons.Length];
        _volumePanel.SetActive(false);
        _modalRoot.SetActive(false);
    }

    void OnEnable()
    {
        _volumeButton.onClick.AddListener(RequestOpen);
        _closeButton.onClick.AddListener(ClosePanel);
        _volumeSlider.onValueChanged.AddListener(ChangeVolume);
    }

    void OnDisable()
    {
        _volumeButton.onClick.RemoveListener(RequestOpen);
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(ClosePanel);
        }
        if (_modalRoot != null)
        {
            _modalRoot.SetActive(false);
        }
        RestoreMenuInput();
        _volumeSlider.onValueChanged.RemoveListener(ChangeVolume);
        PlayerPrefs.Save();
    }

    /// <summary>음량 버튼 입력을 절단 연출 구독자에게 전달하고 완료 후 패널을 연다.</summary>
    private void RequestOpen()
    {
        if (_modalRoot.activeSelf)
        {
            return;
        }
        if (OpenRequested != null)
        {
            OpenRequested(_volumeButton, OpenPanel);
        }
        else
        {
            OpenPanel();
        }
    }

    /// <summary>검은 배경과 음량 패널을 표시하고 닫기 버튼에 UI 입력 포커스를 지정한다.</summary>
    private void OpenPanel()
    {
        for (int i = 0; i < _menuButtons.Length; i++)
        {
            _menuButtonStates[i] = _menuButtons[i].interactable;
            _menuButtons[i].interactable = false;
        }
        _menuInputBlocked = true;
        _modalRoot.transform.SetAsLastSibling();
        _modalRoot.SetActive(true);
        _volumePanel.SetActive(true);
        if (_eventSystem != null)
        {
            _eventSystem.SetSelectedGameObject(_closeButton.gameObject);
        }
    }

    /// <summary>음량 UI를 닫고 저장한 뒤 메인 메뉴 복귀 이벤트를 알린다.</summary>
    private void ClosePanel()
    {
        _volumePanel.SetActive(false);
        _modalRoot.SetActive(false);
        PlayerPrefs.Save();
        RestoreMenuInput();
        PanelClosed?.Invoke();
        if (_eventSystem != null)
        {
            _eventSystem.SetSelectedGameObject(_volumeButton.gameObject);
        }
    }

    /// <summary>음량 패널 진입 전에 저장한 메인 메뉴 버튼의 입력 가능 상태를 복원한다.</summary>
    private void RestoreMenuInput()
    {
        if (!_menuInputBlocked)
        {
            return;
        }
        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] != null)
            {
                _menuButtons[i].interactable = _menuButtonStates[i];
            }
        }
        _menuInputBlocked = false;
    }

    /// <summary>기존 음량 패널을 검은 전체 화면 배경 안에 배치하고 닫기 버튼을 생성한다.</summary>
    private void CreateModal()
    {
        Canvas canvas = _volumePanel.GetComponentInParent<Canvas>();
        _panelContainer = _volumePanel.transform.parent.gameObject;
        _modalRoot = new GameObject("VolumeModal", typeof(RectTransform), typeof(Image));
        _modalRoot.layer = canvas.gameObject.layer;
        RectTransform root = (RectTransform)_modalRoot.transform;
        root.SetParent(canvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = Vector2.zero;
        Image backdrop = _modalRoot.GetComponent<Image>();
        backdrop.color = Color.black;
        backdrop.raycastTarget = true;
        _panelContainer.transform.SetParent(root, true);

        GameObject closeObject = new GameObject("VolumeCloseButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        closeObject.layer = canvas.gameObject.layer;
        RectTransform closeRect = (RectTransform)closeObject.transform;
        closeRect.SetParent(root, false);
        closeRect.anchorMin = Vector2.one;
        closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.sizeDelta = new Vector2(120f, 48f);
        closeRect.anchoredPosition = new Vector2(-32f, -32f);
        Image closeImage = closeObject.GetComponent<Image>();
        closeImage.color = new Color(0.2f, 0.16f, 0.1f, 1f);
        _closeButton = closeObject.GetComponent<Button>();
        _closeButton.targetGraphic = closeImage;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = canvas.gameObject.layer;
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(closeRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = _buttonText.font;
        label.text = "닫기";
        label.fontSize = 26f;
        label.color = new Color(1f, 0.9f, 0.7f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    /// <summary>volume을 0~1로 제한하여 전체 음량에 반영하고 저장값을 갱신한다.</summary>
    private void ChangeVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        ApplyVolume(volume);
        PlayerPrefs.SetFloat(VOLUME_PREF_KEY, volume);
    }

    /// <summary>volume을 BGM·효과음과 Unity AudioListener의 전체 음량에 적용한다.</summary>
    private void ApplyVolume(float volume)
    {
        Managers.Sound.BgmVolume = volume;
        Managers.Sound.EffectVolume = volume;
        AudioListener.volume = volume;
    }
}
