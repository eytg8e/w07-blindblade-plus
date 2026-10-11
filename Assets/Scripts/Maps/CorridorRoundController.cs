using UnityEngine;

/// <summary>기존 복도 호출자를 HP 기반 게임 상태 매니저로 연결한다.</summary>
public sealed class CorridorRoundController : MonoBehaviour
{
    private GameStateManager _state;
    public bool IsGameOver => _state != null && _state.State == GameplayState.GameOver;

    /// <summary>state를 보관한다. 피격·사망 판단은 PlayerHealth와 GameStateManager가 담당한다.</summary>
    public void Initialize(GameStateManager state)
    {
        _state = state;
    }

    /// <summary>보관한 상태 매니저에 현재 씬 재시작을 요청한다. 반환값 없이 상태 매니저가 처리한다.</summary>
    public void Restart()
    {
        if (_state != null) _state.Restart();
    }

    /// <summary>외부 입력 없이 보관한 게임 상태 매니저 참조를 해제한다.</summary>
    public void Shutdown()
    {
        _state = null;
    }

    void OnDisable()
    {
        Shutdown();
    }
}

