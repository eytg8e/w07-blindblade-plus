using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.InputSystem;

/// <summary>엔딩 독백을 순서대로 표시하고 검은 화면으로 페이드한 뒤 메뉴 씬으로 돌아간다.</summary>
[DisallowMultipleComponent]
public sealed class EndingSceneController : MonoBehaviour
{
    [Header("엔딩 독백")]
    [SerializeField] private MonologueData _monologue;
    [SerializeField] private MonologueUI _ui;

    [Header("페이드 및 복귀")]
    [SerializeField] private CanvasGroup _fade;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 2f;
    [SerializeField] private string _menuSceneName = "IntroScene";
    [SerializeField] private SceneTransitionFade _entryFade;

    private int _lineIndex;
    private int _shownFrame;
    private float _elapsed;
    private bool _fading;
    private bool _loading;
    private bool _started;

    void Start()
    {
        if (_monologue == null || _monologue.LineCount == 0 || _ui == null || _fade == null)
        {
            Debug.LogError("엔딩 독백 데이터, UI 및 Fade 참조를 확인하세요.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < _monologue.LineCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(_monologue.GetLine(i))) continue;
            Debug.LogError($"엔딩 독백의 {i + 1}번째 대사가 비어 있습니다.", this);
            enabled = false;
            return;
        }
        _ui.Initialize();
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _fade.interactable = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _started = true;
        ShowLine();
    }

    void Update()
    {
        if (!_started || _loading || (_entryFade != null && _entryFade.IsFading)) return;
        if (_fading)
        {
            _elapsed += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _fadeDuration));
            if (_fade.alpha >= 1f)
            {
                _loading = true;
                StartCoroutine(ReturnToMenu());
            }
            return;
        }
        bool next = Application.isFocused && Time.frameCount != _shownFrame
            && ((Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame
                    || Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        if (next) Advance();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_started && _ui != null) _ui.SetVisible(false);
        _started = false;
    }

    /// <summary>현재 대사 번호의 문구와 마지막 여부를 기존 UI에 표시하고 같은 프레임의 중복 진행을 방지한다.</summary>
    private void ShowLine()
    {
        _elapsed = 0f;
        _shownFrame = Time.frameCount;
        _ui.Show(_monologue.GetLine(_lineIndex), _lineIndex == _monologue.LineCount - 1);
    }

    /// <summary>다음 대사를 표시하거나 마지막 대사가 끝나면 독백을 숨기고 페이드아웃을 시작한다.</summary>
    private void Advance()
    {
        _lineIndex++;
        if (_lineIndex < _monologue.LineCount)
        {
            ShowLine();
            return;
        }
        _ui.SetVisible(false);
        _fading = true;
        _elapsed = 0f;
        _fade.blocksRaycasts = true;
    }

    /// <summary>완전히 검어진 화면을 한 프레임 유지한 뒤 등록된 메뉴 씬으로 비동기 전환한다.</summary>
    private IEnumerator ReturnToMenu()
    {
        yield return null;
        if (!Application.CanStreamedLevelBeLoaded(_menuSceneName))
        {
            Debug.LogError("메뉴 씬을 Build Settings에 등록하세요: " + _menuSceneName, this);
            yield break;
        }
        Time.timeScale = 1f;
        IntroFadeInController.RequestFadeIn();
        SceneManager.LoadSceneAsync(_menuSceneName);
    }
}
