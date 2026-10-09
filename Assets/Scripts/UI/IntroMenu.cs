using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroMenu : MonoBehaviour
{
    private const string VOLUME_PREF_KEY = "IntroMenu.Volume";

    [Header("Menu")]
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private UnityEngine.UI.Button volumeButton;
    [SerializeField] private UnityEngine.UI.Button quitButton;
    [SerializeField] private string _gameSceneName = "SampleScene";

    [Header("Volume")]
    [SerializeField] private GameObject _volumePanel;
    [SerializeField] private UnityEngine.UI.Slider _volumeSlider;

    void Awake()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_PREF_KEY, 1f));
        _volumeSlider.SetValueWithoutNotify(volume);
        ApplyVolume(volume);
        _volumePanel.SetActive(false);
    }

    void OnEnable()
    {
        startButton.onClick.AddListener(OnStartClicked);
        volumeButton.onClick.AddListener(OnVolumeClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    void OnDisable()
    {
        startButton.onClick.RemoveListener(OnStartClicked);
        volumeButton.onClick.RemoveListener(OnVolumeClicked);
        quitButton.onClick.RemoveListener(OnQuitClicked);
        _volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Inspector의 게임 씬 이름으로 전환한다.
    /// 빌드에 등록된 씬이면 저장된 음량을 유지하고 게임 씬을 로드한다.
    /// </summary>
    private void OnStartClicked()
    {
        if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            Debug.LogError("[IntroMenu] Game scene is not in the build scene list: " + _gameSceneName);
            return;
        }

        PlayerPrefs.Save();
        SceneManager.LoadScene(_gameSceneName);
    }

    /// <summary>
    /// 볼륨 버튼 입력으로 기존 음량 패널의 활성 상태를 반전한다.
    /// 패널을 닫을 때 현재 음량 설정을 디스크에 저장한다.
    /// </summary>
    private void OnVolumeClicked()
    {
        _volumePanel.SetActive(!_volumePanel.activeSelf);
        if (!_volumePanel.activeSelf)
            PlayerPrefs.Save();
    }

    /// <summary>
    /// 슬라이더의 volume 입력을 0~1 범위로 적용한다.
    /// BGM과 효과음 음량을 갱신하고 다음 실행을 위한 설정값을 기록한다.
    /// </summary>
    private void OnVolumeChanged(float volume)
    {
        volume = Mathf.Clamp01(volume);
        ApplyVolume(volume);
        PlayerPrefs.SetFloat(VOLUME_PREF_KEY, volume);
    }

    /// <summary>
    /// volume을 기존 사운드 매니저의 BGM과 효과음에 함께 적용한다.
    /// Unity AudioSource의 전역 음량도 같은 값으로 변경한다.
    /// </summary>
    private void ApplyVolume(float volume)
    {
        Managers.Sound.BgmVolume = volume;
        Managers.Sound.EffectVolume = volume;
        AudioListener.volume = volume;
    }

    /// <summary>
    /// 종료 버튼 입력으로 음량 설정을 저장하고 게임을 종료한다.
    /// Unity Editor에서는 실행 중인 Play Mode를 종료한다.
    /// </summary>
    private void OnQuitClicked()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
}
    
