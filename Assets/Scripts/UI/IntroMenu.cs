using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class IntroMenu : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button volumeButton;
    [SerializeField] private Button quitButton;

    private int nextIndex;

    private void OnEnable()
    {
        startButton.onClick.AddListener(OnStartClicked);
        volumeButton.onClick.AddListener(OnVolumeClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
         nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
    }

    private void OnDisable()
    {
        startButton.onClick.RemoveListener(OnStartClicked);
        volumeButton.onClick.RemoveListener(OnVolumeClicked);
        quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void OnStartClicked()
    {
        SceneManager.LoadScene(nextIndex);
    }

    private void OnVolumeClicked()
    {
        
    }

    private void OnQuitClicked()
    {
        Application.Quit();
    }
    
}
    