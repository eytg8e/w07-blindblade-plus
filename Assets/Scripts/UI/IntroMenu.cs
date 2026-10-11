using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroMenu : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private UnityEngine.UI.Button quitButton;
    [SerializeField] private string _gameSceneName = "SampleScene";

    void OnEnable()
    {
        startButton.onClick.AddListener(OnStartClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnDisable()
    {
        startButton.onClick.RemoveListener(OnStartClicked);
        quitButton.onClick.RemoveListener(OnQuitClicked);
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
    
