using UnityEngine;

using UnityEngine.UI;

/// <summary>음량 패널의 표시와 전체 음량의 복원·변경·저장을 담당한다.</summary>
public sealed class VolumeControlUI : MonoBehaviour
{
    private const string VOLUME_PREF_KEY = "IntroMenu.Volume";

    [Header("음량 UI")]
    [SerializeField] private Button _volumeButton;
    [SerializeField] private GameObject _volumePanel;
    [SerializeField] private Slider _volumeSlider;

    void Awake()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_PREF_KEY, 1f));
        _volumeSlider.SetValueWithoutNotify(volume);
        ApplyVolume(volume);
        _volumePanel.SetActive(false);
    }

    void OnEnable()
    {
        _volumeButton.onClick.AddListener(TogglePanel);
        _volumeSlider.onValueChanged.AddListener(ChangeVolume);
    }

    void OnDisable()
    {
        _volumeButton.onClick.RemoveListener(TogglePanel);
        _volumeSlider.onValueChanged.RemoveListener(ChangeVolume);
        PlayerPrefs.Save();
    }

    /// <summary>음량 패널의 표시 상태를 반전하고 닫을 때 설정을 저장한다.</summary>
    private void TogglePanel()
    {
        _volumePanel.SetActive(!_volumePanel.activeSelf);
        if (!_volumePanel.activeSelf)
            PlayerPrefs.Save();
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
