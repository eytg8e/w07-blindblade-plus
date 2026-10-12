using UnityEngine;

using UnityEngine.InputSystem;

/// <summary>현재 씬의 공통 독백 화면에서 한 독백의 대사 순서와 진행 입력을 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIManager))]
public sealed class MonologueController : MonoBehaviour
{
    [Header("대사 진행")]
    [SerializeField] private Key _advanceKey = Key.Enter;

    private UIManager _manager;
    private MonologueUI _ui;
    private MonologueData _data;
    private int _lineIndex;
    private int _startedFrame;

    public bool IsPlaying => _data != null;

    void Awake()
    {
        _manager = GetComponent<UIManager>();
    }

    void Update()
    {
        if (!IsPlaying) return;
        if (!Game.IsInitialized || Game.Instance.UIManager != _manager
            || Game.Instance.GameStateManager == null
            || Game.Instance.GameStateManager.State != GameplayState.Playing || _ui == null)
        {
            Stop();
            return;
        }
        if (!Application.isFocused || Game.Player == null || Game.Player.IsPaused) return;
        if (_advanceKey != Key.None && Keyboard.current != null
            && Keyboard.current[_advanceKey].wasPressedThisFrame)
            Advance();
    }

    void OnDisable()
    {
        Stop();
    }

    /// <summary>data의 유효한 대사를 현재 준비된 세션에서 시작한다. 이미 재생 중이거나 준비되지 않으면 false를 반환한다.</summary>
    public bool TryPlay(MonologueData data)
    {
        if (!isActiveAndEnabled || IsPlaying || data == null || data.LineCount == 0
            || !Game.IsInitialized || Game.Instance.UIManager != _manager
            || Game.Instance.GameStateManager == null
            || Game.Instance.GameStateManager.State != GameplayState.Playing)
            return false;
        for (int i = 0; i < data.LineCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(data.GetLine(i))) continue;
            Debug.LogError($"독백 데이터의 {i + 1}번째 대사가 비어 있습니다: {data.name}", data);
            return false;
        }
        _ui = _manager.Get<MonologueUI>();
        if (_ui == null) return false;
        _data = data;
        _lineIndex = 0;
        _startedFrame = Time.frameCount;
        ShowCurrentLine();
        return true;
    }

    /// <summary>재생 중 다음 대사를 표시한다. 마지막 대사 이후 호출되면 독백을 종료한다.</summary>
    public void Advance()
    {
        if (!IsPlaying || Time.frameCount == _startedFrame) return;
        _lineIndex++;
        if (_lineIndex >= _data.LineCount)
        {
            Stop();
            return;
        }
        ShowCurrentLine();
    }

    /// <summary>현재 대사와 마지막 여부를 기존 MonologueUI로 전달하여 표시한다.</summary>
    private void ShowCurrentLine()
    {
        _ui.Show(_data.GetLine(_lineIndex), _lineIndex == _data.LineCount - 1);
    }

    /// <summary>현재 독백 화면을 숨기고 데이터 및 UI 참조를 해제한다. 매니저를 생성하거나 입력 정책을 변경하지 않는다.</summary>
    public void Stop()
    {
        if (_data != null && _ui != null) _ui.SetVisible(false);
        _data = null;
        _ui = null;
        _lineIndex = 0;
    }
}
