using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬의 전체 화면 오버레이로 진입 페이드인과 전환 페이드아웃을 처리한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class SceneTransitionFade : MonoBehaviour
{
    private static int _pendingBuildIndex = -1;

    [Header("페이드")]
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private bool _fadeOnEntry;
    [SerializeField, Min(.01f)] private float _fadeInDuration = 2f;
    private bool _entryRequested;
    private bool _ownsPause;
    private bool _pausedPlayer;
    private float _previousTimeScale;
    private bool _previousInput;
    private PlayerController _player;
    private InputManager _input;
    private GameStateManager _state;

    public bool IsFading { get; private set; }

    /// <summary>새 실행 세션에서 이전 씬 전환 요청을 초기화한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRequest() { _pendingBuildIndex = -1; }

    /// <summary>buildIndex에 해당하는 다음 씬의 페이드인을 요청한다.</summary>
    public static void RequestEntry(int buildIndex) { _pendingBuildIndex = buildIndex; }

    void Awake()
    {
        _entryRequested = _fadeOnEntry || _pendingBuildIndex == gameObject.scene.buildIndex;
        if (_pendingBuildIndex == gameObject.scene.buildIndex) _pendingBuildIndex = -1;
        _overlay.alpha = _entryRequested ? 1f : 0f;
        _overlay.blocksRaycasts = _entryRequested;
        _overlay.interactable = false;
        IsFading = _entryRequested;
    }

    void Start()
    {
        if (_entryRequested) StartCoroutine(FadeIn());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        RestoreEntryPause();
        IsFading = false;
    }

    /// <summary>진입 중 게임 세션이 있으면 잠시 정지하고 페이드인 완료 후 기존 시간과 입력을 복구한다.</summary>
    private IEnumerator FadeIn()
    {
        if (Game.IsInitialized && Game.Instance.GameStateManager != null
            && Game.Instance.GameStateManager.State == GameplayState.Playing && Time.timeScale > 0f)
        {
            _state = Game.Instance.GameStateManager;
            _input = Game.Instance.InputManager;
            _player = Game.Player;
            _previousTimeScale = Time.timeScale;
            _previousInput = _input.IsGameplayInputEnabled;
            _ownsPause = true;
            _input.SetGameplayInputEnabled(false);
            _pausedPlayer = _player != null && !_player.IsPaused;
            if (_pausedPlayer) _player.SetPaused(true);
            Time.timeScale = 0f;
        }
        yield return FadeTo(0f, _fadeInDuration);
        RestoreEntryPause();
    }

    /// <summary>duration초 동안 오버레이를 완전히 검게 만든다. 진입 중에는 호출하지 않는다.</summary>
    public IEnumerator FadeOut(float duration)
    {
        yield return FadeTo(1f, duration);
    }

    /// <summary>target 알파까지 unscaled 시간으로 부드럽게 변경하고 페이드 상태를 갱신한다.</summary>
    private IEnumerator FadeTo(float target, float duration)
    {
        IsFading = true;
        _overlay.blocksRaycasts = true;
        float start = _overlay.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _overlay.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration))));
            yield return null;
        }
        _overlay.alpha = target;
        _overlay.blocksRaycasts = target > 0f;
        IsFading = false;
    }

    /// <summary>이 컴포넌트가 소유한 진입 일시정지만 복구하고 게임오버의 시간 정지는 유지한다.</summary>
    private void RestoreEntryPause()
    {
        if (!_ownsPause) return;
        _ownsPause = false;
        if (_state == null || (_state.State != GameplayState.GameOver && _state.State != GameplayState.Restarting))
            Time.timeScale = _previousTimeScale;
        if (_pausedPlayer && _player != null) _player.SetPaused(false);
        if (_input != null) _input.SetGameplayInputEnabled(_previousInput);
    }

    /// <summary>취소된 씬 이동의 검은 오버레이를 제거한다.</summary>
    public void Clear()
    {
        _overlay.alpha = 0f;
        _overlay.blocksRaycasts = false;
        IsFading = false;
    }
}
