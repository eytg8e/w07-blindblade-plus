using UnityEngine;

using UnityEngine.UI;

/// <summary>게임오버 상태에 따른 화면 표시와 버튼 동작을 담당한다.</summary>
public sealed class GameOverUI : GameUI
{
    [Header("게임오버 UI")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _stayButton;
    private GameStateManager _state;

    /// <summary>Game의 상태 매니저를 캐싱하고 상태 구독과 버튼 및 초기 표시를 연결한다.</summary>
    public override void Initialize()
    {
        Shutdown();
        _state = Game.Instance.GameStateManager;
        _state.StateChanged += HandleState;
        _restartButton.onClick.AddListener(Restart);
        _stayButton.onClick.AddListener(Stay);
        HandleState(_state.State);
    }

    /// <summary>visible에 따라 게임오버 패널을 표시하고 재시작 버튼을 선택한다.</summary>
    public override void SetVisible(bool visible)
    {
        base.SetVisible(visible);
        _panel.SetActive(visible);
        if (visible) _restartButton.Select();
    }

    /// <summary>state가 게임오버이면 화면을 표시하고 그 외에는 숨긴다.</summary>
    private void HandleState(GameplayState state)
    {
        SetVisible(state == GameplayState.GameOver);
    }

    /// <summary>현재 상태 매니저에 재시작을 요청한다.</summary>
    private void Restart()
    {
        _state.Restart();
    }

    /// <summary>화면을 유지하고 재시작 버튼을 선택한다.</summary>
    private void Stay()
    {
        _restartButton.Select();
    }

    /// <summary>버튼 요청 연결과 현재 상태 참조를 해제한다.</summary>
    public override void Shutdown()
    {
        if (_state == null) return;
        _state.StateChanged -= HandleState;
        _restartButton.onClick.RemoveListener(Restart);
        _stayButton.onClick.RemoveListener(Stay);
        _state = null;
    }

    void OnDestroy()
    {
        Shutdown();
    }
}
