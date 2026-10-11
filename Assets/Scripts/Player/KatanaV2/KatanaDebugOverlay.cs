using System.Collections.Generic;
using System.Text;

using UnityEngine;


/// <summary>기존 플레이어 로그를 보완하는 카타나 상태 표시다. 게임 판정은 변경하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class KatanaDebugOverlay : MonoBehaviour
{
    [Header("Debug switches")]
    [SerializeField] private bool _showOverlay = true;
    [SerializeField] private bool _logEvents = true;

    private readonly Queue<string> _recent = new Queue<string>();
    private readonly StringBuilder _text = new StringBuilder(600);
    private KatanaCombatCoordinator _combat;
    private KatanaWeaponVisual _visual;
    private KatanaDamageReceiver _receiver;
    private string _lastResult = "No hit yet";

    void Awake()
    {
        _combat = GetComponent<KatanaCombatCoordinator>();
        _visual = GetComponent<KatanaWeaponVisual>();
        _receiver = GetComponent<KatanaDamageReceiver>();
    }

    void OnEnable()
    {
        _combat.EventRaised += HandleEvent;
        _combat.Player.ActionEvent += HandlePlayer;
        _receiver.DamageReceived += HandleDamage;
    }

    void OnDisable()
    {
        _combat.EventRaised -= HandleEvent;
        _combat.Player.ActionEvent -= HandlePlayer;
        _receiver.DamageReceived -= HandleDamage;
    }

    void OnGUI()
    {
        if (!_showOverlay) return;
        _text.Clear();
        _text.AppendLine("MELEE | LMB attack / RMB guard + timed parry / Ctrl + C crouch / Esc cursor");
        _text.Append("Action: ").Append(_combat.Player.CurrentState)
            .Append("  Recovery: ").AppendLine(_combat.Player.RecoveryRemaining.ToString("0.00"));
        _text.Append("Weapon: ").Append(_combat.WeaponState).AppendLine("  Attack: Iaido single-hit");
        _text.Append("Parry: ").Append(_combat.IsParryMotion ? "SUCCESS | continuing slash " : _combat.IsParryWindowOpen ? "OPEN until hit " : "closed ")
            .Append(_combat.ParryRemaining.ToString("0.00")).Append("  Crouch: ")
            .AppendLine(_combat.Player.IsCrouching ? "on" : "off");
        _text.Append("Animator: ").AppendLine(_visual.AnimatorState);
        _text.AppendLine(_lastResult);
        foreach (string line in _recent) _text.AppendLine(line);
        GUI.Box(new Rect(12f, 12f, 575f, 265f), GUIContent.none);
        GUI.Label(new Rect(24f, 22f, 550f, 245f), _text.ToString());
    }

    /// <summary>카타나 주요 이벤트 e를 최근 목록에 추가하고 설정에 따라 한 번만 로그를 남긴다.</summary>
    private void HandleEvent(KatanaEvent e)
    {
        string text = e.Stage + " | strike=" + e.Strike + " | " + e.Detail;
        _recent.Enqueue(text);
        while (_recent.Count > 5) _recent.Dequeue();
        if (_logEvents) Debug.Log("[Katana] " + text, this);
    }

    /// <summary>기존 판정 이벤트 e의 결과만 표시한다. 기존 PlayerDebugOverlay가 전투 Console 로그를 담당한다.</summary>
    private void HandlePlayer(PlayerActionEvent e)
    {
        if (e.Stage == PlayerActionStage.HitConfirmed || e.Stage == PlayerActionStage.Missed || e.Stage == PlayerActionStage.DeathConfirmed)
            _lastResult = "Outgoing: " + e.Stage + " | action=" + e.ActionId;
    }

    /// <summary>차단되지 않은 context를 표시한다. 플레이어 체력은 이 디버그 컴포넌트에서 변경하지 않는다.</summary>
    private void HandleDamage(DamageContext context)
    {
        _lastResult = "Incoming damage forwarded: " + context.Amount + " | " + context.PatternId;
        if (_logEvents) Debug.Log("[Katana] " + _lastResult, this);
    }
}
