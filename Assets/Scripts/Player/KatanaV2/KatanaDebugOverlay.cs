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

    [Header("전투 성공 피드백")]
    [SerializeField] private bool _showCombatFeedback = true;
    [SerializeField, Min(0.1f)] private float _feedbackDuration = 0.8f;
    private GUIStyle _feedbackStyle;
    private string _feedbackNotice;
    private string _lastDefense = "아직 방어 판정 없음";
    private bool _lastWasParry;
    private float _feedbackUntil;

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
        _combat.DefenseSucceeded += HandleDefense;
    }

    void OnDisable()
    {
        _combat.EventRaised -= HandleEvent;
        _combat.Player.ActionEvent -= HandlePlayer;
        _receiver.DamageReceived -= HandleDamage;
        _combat.DefenseSucceeded -= HandleDefense;
    }

    void OnGUI()
    {
        // 모든 씬에서 가드·패링·반격 안내 텍스트 표시를 비활성화한다.
        // if (_showCombatFeedback) DrawCombatFeedback();
        if (!_showOverlay) return;
        _text.Clear();
        _text.AppendLine("MELEE | LMB attack / RMB guard + timed parry / Ctrl + C crouch / Esc cursor");
        _text.Append("Action: ").Append(_combat.Player.CurrentState)
            .Append("  Recovery: ").AppendLine(_combat.Player.RecoveryRemaining.ToString("0.00"));
        _text.Append("Weapon: ").Append(_combat.WeaponState).Append("  Attack: ").AppendLine(_combat.IsCounterAttack ? "패링 반격" : _combat.Strike + "타");
        _text.Append("Defense: ").Append(_lastDefense).Append(" | timing window: ").Append(_combat.IsParryWindowOpen ? "open " : "closed ")
            .Append(_combat.ParryRemaining.ToString("0.00")).Append("  Crouch: ")
            .AppendLine(_combat.Player.IsCrouching ? "on" : "off");
        _text.Append("Animator: ").AppendLine(_visual.AnimatorState);
        _text.AppendLine(_lastResult);
        foreach (string line in _recent) _text.AppendLine(line);
        // GUI.Box(new Rect(12f, 12f, 575f, 265f), GUIContent.none);
        // GUI.Label(new Rect(24f, 22f, 550f, 245f), _text.ToString());
    }

    /// <summary>실제 방어 성공 result의 종류를 기록하고 짧은 성공 알림을 예약한다. 게임 판정은 변경하지 않는다.</summary>
    private void HandleDefense(KatanaDefenseResult result)
    {
        _lastWasParry = result.IsParry;
        _lastDefense = result.IsParry ? "패링 성공" : "가드 성공";
        _feedbackNotice = _lastDefense;
        _feedbackUntil = Time.time + _feedbackDuration;
    }

    /// <summary>현재 가드·실제 성공·반격 기회를 화면 아래 중앙에 표시한다. 기술 디버그 패널이 꺼져 있어도 설정에 따라 표시한다.</summary>
    private void DrawCombatFeedback()
    {
        string notice;
        Color color = Color.white;
        if (_combat.IsCounterAttack) { notice = "반격"; color = Color.cyan; }
        else if (_combat.CanCounterAttack)
        {
            notice = "패링 성공 · 좌클릭 반격  " + _combat.CounterRemaining.ToString("0.0") + "초";
            color = Color.cyan;
        }
        else if (Time.time < _feedbackUntil)
        {
            notice = _lastWasParry ? _feedbackNotice + " · 반격 기회 종료" : _feedbackNotice;
            color = _lastWasParry ? Color.cyan : Color.white;
        }
        else if (_combat.Player.IsGuarding) notice = _combat.IsParryWindowOpen ? "가드 · 패링 타이밍" : "가드 중";
        else return;
        if (_feedbackStyle == null)
            _feedbackStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        Rect area = new Rect((Screen.width - 440f) * 0.5f, Screen.height * 0.68f, 440f, 44f);
        Color previous = GUI.color;
        GUI.Box(area, GUIContent.none);
        GUI.color = color;
        GUI.Label(area, notice, _feedbackStyle);
        GUI.color = previous;
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
