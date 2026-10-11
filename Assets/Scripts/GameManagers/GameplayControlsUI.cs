using UnityEngine;

using UnityEngine.UI;

/// <summary>입력 및 상태 요청 버튼의 화면 연결을 담당한다.</summary>
public sealed class GameplayControlsUI : GameUI
{
    [Header("조작 버튼")]
    [SerializeField] private Button _blockInputButton;
    [SerializeField] private Button _allowInputButton;
    [SerializeField] private Button _gameOverButton;
    [SerializeField] private Button _restartButton;
    private GameStateManager _state;
    private InputManager _input;

    /// <summary>Game의 상태·입력 매니저를 캐싱하고 버튼 요청을 연결한다.</summary>
    public override void Initialize()
    {
        Shutdown();
        _state = Game.Instance.GameStateManager;
        _input = Game.Instance.InputManager;
        _blockInputButton.onClick.AddListener(BlockInput);
        _allowInputButton.onClick.AddListener(AllowInput);
        _gameOverButton.onClick.AddListener(RequestGameOver);
        _restartButton.onClick.AddListener(Restart);
    }

    /// <summary>현재 입력 매니저에 입력 차단을 요청한다.</summary>
    private void BlockInput() { _input.SetGameplayInputEnabled(false); }

    /// <summary>플레이 중인 경우 현재 입력 매니저에 입력 허용을 요청한다.</summary>
    private void AllowInput()
    {
        if (_state.State == GameplayState.Playing) _input.SetGameplayInputEnabled(true);
    }

    /// <summary>현재 상태 매니저에 게임오버 전환을 요청한다.</summary>
    private void RequestGameOver() { _state.RequestGameOver(); }

    /// <summary>현재 상태 매니저에 재시작을 요청한다.</summary>
    private void Restart() { _state.Restart(); }

    /// <summary>버튼 구독을 해제하고 매니저 참조를 정리한다.</summary>
    public override void Shutdown()
    {
        if (_state == null) return;
        _blockInputButton.onClick.RemoveListener(BlockInput);
        _allowInputButton.onClick.RemoveListener(AllowInput);
        _gameOverButton.onClick.RemoveListener(RequestGameOver);
        _restartButton.onClick.RemoveListener(Restart);
        _state = null;
        _input = null;
    }

    void OnDestroy() { Shutdown(); }
}
