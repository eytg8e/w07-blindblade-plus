using UnityEngine;


/// <summary>복도의 첫 적중 전 피격을 판정하고 게임 상태 매니저에 패배를 요청한다.</summary>
public sealed class CorridorRoundController : MonoBehaviour
{
    private PlayerController _player;
    private DamageReceiver _damageReceiver;
    private GameStateManager _state;
    private bool _hasHitEnemy;
    public bool IsGameOver => _state != null && _state.State == GameplayState.GameOver;

    /// <summary>현재 플레이어와 state를 연결하고 복도 패배 조건 이벤트를 구독한다.</summary>
    public void Initialize(GameStateManager state)
    {
        Shutdown();
        _player = Game.Player;
        _damageReceiver = _player.GetComponent<DamageReceiver>();
        _state = state;
        _hasHitEnemy = false;
        _player.ActionEvent += HandleAction;
        _damageReceiver.DamageReceived += HandleDamage;
    }

    /// <summary>action의 첫 적중 여부를 기록한다. 일반 사망은 상태 매니저가 처리한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if (action.Stage == PlayerActionStage.HitConfirmed) _hasHitEnemy = true;
    }

    /// <summary>context가 첫 적중 전 적 피해라면 기존 복도 패배 조건을 요청한다.</summary>
    private void HandleDamage(DamageContext context)
    {
        if (!_hasHitEnemy && context.Instigator != null
            && context.Instigator.GetComponentInParent<EnemyHealth>() != null)
            _state.RequestGameOver();
    }

    /// <summary>외부의 기존 재시작 요청을 게임 상태 매니저에 전달한다.</summary>
    public void Restart()
    {
        if (_state != null) _state.Restart();
    }

    /// <summary>플레이어 행동과 피해 이벤트 구독을 해제한다.</summary>
    public void Shutdown()
    {
        if (_player != null) _player.ActionEvent -= HandleAction;
        if (_damageReceiver != null) _damageReceiver.DamageReceived -= HandleDamage;
        _player = null;
        _damageReceiver = null;
        _state = null;
    }

    void OnDisable()
    {
        Shutdown();
    }
}

