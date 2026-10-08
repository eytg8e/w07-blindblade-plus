using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Test-only HUD and event logger for the player sandbox. It observes existing player events
/// and never changes combat, perception, or movement results.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDebugOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private PlayerTestWorldAdapter _testWorld;

    [Header("Debug Output")]
    [SerializeField] private bool _showOverlay = true;
    [SerializeField] private bool _logEvents = true;
    [SerializeField] private bool _logFootsteps = true;
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField, Min(0.05f)] private float _targetRefreshInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float _feedbackDuration = 0.65f;

    private const int _maxRecentEvents = 5;
    private readonly Queue<string> _recentEvents = new Queue<string>();
    private readonly List<Vector3> _detectedPositions = new List<Vector3>();
    private readonly StringBuilder _displayBuilder = new StringBuilder(512);

    private string _detectedTargets = "waiting for test perception";
    private string _latestAction = "No player action events yet";
    private string _latestStimulus = "No stimulus event yet";
    private string _feedbackText;
    private Color _feedbackColor = Color.white;
    private float _refreshRemaining;
    private float _feedbackRemaining;
    private float _recoveryStartedAt;
    private float _recoveryDuration;
    private PlayerAttackOutcome _recoveryOutcome;
    private string _lastRecoverySummary = "No timed recovery measured yet";
    private Vector3 _lastFootstepPosition;
    private PlayerFootstepType _lastFootstepType;
    private bool _hasFootstep;
    private GUIStyle _headerStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _bannerStyle;

    private void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_motor == null)
            _motor = GetComponent<PlayerMotor>();
    }

    private void OnEnable()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_motor == null)
            _motor = GetComponent<PlayerMotor>();

        if (_player != null)
        {
            _player.ActionEvent += HandleActionEvent;
            _player.FootstepEmitted += HandleFootstep;
            _player.SenseContextChanged += HandleSenseContextChanged;
        }

        _refreshRemaining = 0f;
        RefreshDetectedTargets();
    }

    private void OnDisable()
    {
        if (_player == null)
            return;

        _player.ActionEvent -= HandleActionEvent;
        _player.FootstepEmitted -= HandleFootstep;
        _player.SenseContextChanged -= HandleSenseContextChanged;
    }

    private void Update()
    {
        _refreshRemaining -= Time.unscaledDeltaTime;
        if (_refreshRemaining <= 0f)
        {
            _refreshRemaining = _targetRefreshInterval;
            RefreshDetectedTargets();
        }

        _feedbackRemaining = Mathf.Max(0f, _feedbackRemaining - Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        if (!_showOverlay)
            return;

        EnsureStyles();

        Color previousColor = GUI.color;
        GUI.color = new Color(0.015f, 0.02f, 0.03f, 0.82f);
        float recoveryDetailsHeight = _player != null
            && (_player.IsInRecovery
                || (_player.CurrentState == PlayerActionState.Paused && _player.RecoveryRemaining > 0f))
            ? 38f
            : 0f;
        float panelHeight = 269f + _recentEvents.Count * 19f + recoveryDetailsHeight;
        GUI.DrawTexture(new Rect(12f, 12f, 500f, panelHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(24f, 20f, 480f, 26f), "PLAYER / COMBAT DEBUG", _headerStyle);

        _displayBuilder.Clear();
        if (_player == null)
        {
            _displayBuilder.AppendLine("PlayerController: NOT CONNECTED");
        }
        else
        {
            _displayBuilder.Append("Player state: ").Append(_player.CurrentState);
            if (_player.IsCharging)
                _displayBuilder.Append(_player.IsChargingReady ? " (READY)" : " (CHARGING)");
            _displayBuilder.Append(" | action ").AppendLine(_player.CurrentActionId.ToString());
            _displayBuilder.Append("Guard: ").AppendLine(_player.IsGuarding ? "ACTIVE (hold RMB)" : "inactive");
            if (_player.IsInRecovery)
            {
                _displayBuilder.Append("Recovery: ")
                    .Append(_player.RecoveryRemaining.ToString("0.00"))
                    .Append(" / ").Append(_recoveryDuration.ToString("0.00"))
                    .Append(" s | ").AppendLine(_recoveryOutcome.ToString());
                _displayBuilder.AppendLine("Recovery locked: Move / Attack / Guard / Charge / Iaido");
            }
            else if (_player.CurrentState == PlayerActionState.Paused && _player.RecoveryRemaining > 0f)
            {
                _displayBuilder.Append("Recovery paused: ")
                    .Append(_player.RecoveryRemaining.ToString("0.00"))
                    .Append(" / ").Append(_recoveryDuration.ToString("0.00"))
                    .Append(" s | ").AppendLine(_recoveryOutcome.ToString());
                _displayBuilder.AppendLine("Input locked: PAUSED");
            }
        }

        _displayBuilder.Append("Move speed: ")
            .Append(_motor != null ? _motor.HorizontalSpeed.ToString("0.00") : "--")
            .Append(" | sense: ")
            .AppendLine(_testWorld != null ? _testWorld.LastSenseContext.LocomotionMode.ToString() : "NOT CONNECTED");
        _displayBuilder.Append("Test perception: ").AppendLine(_detectedTargets);
        _displayBuilder.AppendLine("Enemy AI: NOT IMPLEMENTED / NOT CONNECTED");
        _displayBuilder.AppendLine("Stimulus source: synthetic test targets; no sound/AI hearing result");
        _displayBuilder.Append("Last stimulus: ").AppendLine(_latestStimulus);
        _displayBuilder.Append("Last action: ").AppendLine(_latestAction);
        _displayBuilder.Append("Recovery timing: ").AppendLine(_lastRecoverySummary);
        _displayBuilder.AppendLine("Recent events:");

        foreach (string eventText in _recentEvents)
            _displayBuilder.Append("  ").AppendLine(eventText);

        GUI.Label(new Rect(24f, 48f, 480f, panelHeight - 58f), _displayBuilder.ToString(), _bodyStyle);

        if (_feedbackRemaining > 0f && !string.IsNullOrEmpty(_feedbackText))
        {
            float alpha = Mathf.Clamp01(_feedbackRemaining / _feedbackDuration);
            GUI.color = new Color(_feedbackColor.r, _feedbackColor.g, _feedbackColor.b, 0.12f * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, 70f, Screen.width, 48f), _feedbackText, _bannerStyle);
        }

        GUI.color = previousColor;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos)
            return;

        PlayerController player = _player != null ? _player : GetComponent<PlayerController>();
        Vector3 origin = player != null ? player.transform.position + Vector3.up * 1.1f : transform.position;

        if (_testWorld != null)
        {
            IReadOnlyList<PlayerPerceivedTarget> targets = _testWorld.GetCurrentTargets();
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            foreach (PlayerPerceivedTarget target in targets)
            {
                Gizmos.DrawWireSphere(target.WorldPosition, 0.3f);
                Gizmos.DrawLine(origin, target.WorldPosition);
            }
        }

        if (_hasFootstep)
        {
            Gizmos.color = _lastFootstepType == PlayerFootstepType.Quiet ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(_lastFootstepPosition, 0.14f);
        }
    }

    /// <summary>플레이어 행동 단계와 판정 결과를 HUD에 저장하고 주요 이벤트를 한 번 기록합니다.</summary>
    private void HandleActionEvent(PlayerActionEvent actionEvent)
    {
        string targetId = string.IsNullOrEmpty(actionEvent.TargetId) ? "-" : actionEvent.TargetId;
        string message = $"t={Time.time:0.00} frame={Time.frameCount} action={actionEvent.ActionId} "
            + $"state={actionEvent.State} stage={actionEvent.Stage} outcome={actionEvent.Outcome} target={targetId}";

        _latestAction = message;
        PushRecentEvent(message);

        if (_logEvents)
            Debug.Log("[PlayerDebug] " + message, this);

        switch (actionEvent.Stage)
        {
            case PlayerActionStage.GuardStarted:
                SetFeedback("GUARD ACTIVE", Color.cyan);
                break;
            case PlayerActionStage.GuardSucceeded:
                SetFeedback("GUARD SUCCESS", Color.green);
                break;
            case PlayerActionStage.GuardEnded:
                SetFeedback("GUARD RELEASED", Color.white);
                break;
            case PlayerActionStage.AttackStarted:
                SetFeedback("ATTACK START", new Color(1f, 0.9f, 0.25f, 1f));
                break;
            case PlayerActionStage.ChargeReady:
                SetFeedback("CHARGE READY", Color.cyan);
                break;
            case PlayerActionStage.DashStarted:
                SetFeedback("IAIDO DASH", new Color(1f, 0.45f, 0.15f, 1f));
                break;
            case PlayerActionStage.SlashStarted:
                SetFeedback("IAIDO SLASH", new Color(1f, 0.65f, 0.15f, 1f));
                break;
            case PlayerActionStage.HitConfirmed:
                SetFeedback("HIT CONFIRMED", Color.green);
                break;
            case PlayerActionStage.Missed:
                SetFeedback("ATTACK MISS", new Color(1f, 0.35f, 0.15f, 1f));
                break;
            case PlayerActionStage.DeathConfirmed:
                SetFeedback("TEST TARGET DEFEATED", new Color(1f, 0.1f, 0.1f, 1f));
                break;
            case PlayerActionStage.DashBlocked:
                SetFeedback("DASH BLOCKED", new Color(0.8f, 0.3f, 1f, 1f));
                break;
            case PlayerActionStage.RecoveryStarted:
                _recoveryStartedAt = Time.time;
                _recoveryDuration = _player != null ? _player.RecoveryRemaining : 0f;
                _recoveryOutcome = actionEvent.Outcome;
                _lastRecoverySummary = $"Measuring {_recoveryOutcome}: {_recoveryDuration:0.00}s configured";
                SetFeedback("RECOVERY", new Color(0.3f, 0.75f, 1f, 1f));
                break;
            case PlayerActionStage.RecoveryEnded:
                float elapsed = Mathf.Max(0f, Time.time - _recoveryStartedAt);
                _lastRecoverySummary = $"{_recoveryOutcome}: {elapsed:0.00}s elapsed / {_recoveryDuration:0.00}s configured";
                SetFeedback("READY", Color.white);
                break;
            case PlayerActionStage.RequestRejected:
                SetFeedback("ACTION REQUEST REJECTED", Color.gray);
                break;
        }
    }

    /// <summary>발걸음 이벤트를 최근 자극으로 표시하고 설정된 경우 발걸음 단위로 기록합니다.</summary>
    private void HandleFootstep(PlayerFootstepEvent footstep)
    {
        _hasFootstep = true;
        _lastFootstepPosition = footstep.WorldPosition;
        _lastFootstepType = footstep.Type;
        _latestStimulus = $"player emitted {footstep.Type} footstep, noise={footstep.NoiseStrength:0.00} (outgoing)";

        if (!_logFootsteps)
            return;

        string message = $"t={Time.time:0.00} footstep={footstep.Type} pos={footstep.WorldPosition} "
            + $"speed={footstep.HorizontalSpeed:0.00} noise={footstep.NoiseStrength:0.00}";
        PushRecentEvent(message);
        Debug.Log("[PlayerDebug][Footstep] " + message, this);
    }

    /// <summary>플레이어 이동·집중·충전 문맥이 바뀐 경우에만 표시와 로그를 갱신합니다.</summary>
    private void HandleSenseContextChanged(PlayerSenseContext context)
    {
        _latestStimulus = $"player context {context.LocomotionMode}, moving={context.IsMoving}, "
            + $"focusing={context.IsFocusing}, charging={context.IsCharging}";

        if (!_logEvents)
            return;

        string message = $"t={Time.time:0.00} sense={context.LocomotionMode} moving={context.IsMoving} "
            + $"focusing={context.IsFocusing} charging={context.IsCharging}";
        PushRecentEvent(message);
        Debug.Log("[PlayerDebug][SenseContext] " + message, this);
    }

    /// <summary>테스트 감지 제공자가 보고하는 대상을 낮은 빈도로 갱신하고 목록 변화만 기록합니다.</summary>
    private void RefreshDetectedTargets()
    {
        if (_testWorld == null)
        {
            _detectedTargets = "NOT CONNECTED";
            _detectedPositions.Clear();
            return;
        }

        IReadOnlyList<PlayerPerceivedTarget> targets = _testWorld.GetCurrentTargets();
        _detectedPositions.Clear();
        StringBuilder builder = new StringBuilder(128);

        if (targets.Count == 0)
            builder.Append("none");
        else
        {
            builder.Append("[TEST AUTO] ");
            for (int i = 0; i < targets.Count; i++)
            {
                PlayerPerceivedTarget target = targets[i];
                if (i > 0)
                    builder.Append(", ");
                builder.Append(target.TargetId)
                    .Append(" @ ")
                    .Append(target.WorldPosition.ToString("F1"));
                _detectedPositions.Add(target.WorldPosition);
            }
        }

        string summary = builder.ToString();
        if (_detectedTargets == summary)
            return;

        _detectedTargets = summary;
        if (_logEvents)
            Debug.Log("[PlayerDebug][Perception] Synthetic test targets changed: " + summary, this);
    }

    /// <summary>최신 이벤트를 제한된 HUD 목록에 추가해 오래된 항목을 밀어냅니다.</summary>
    private void PushRecentEvent(string eventText)
    {
        _recentEvents.Enqueue(eventText);
        while (_recentEvents.Count > _maxRecentEvents)
            _recentEvents.Dequeue();
    }

    /// <summary>짧은 색상 플래시와 배너를 표시할 피드백 문구를 설정합니다.</summary>
    private void SetFeedback(string text, Color color)
    {
        _feedbackText = text;
        _feedbackColor = color;
        _feedbackRemaining = _feedbackDuration;
    }

    /// <summary>게임 뷰용 HUD와 일시적 피드백에 사용할 텍스트 스타일을 한 번 생성합니다.</summary>
    private void EnsureStyles()
    {
        if (_bodyStyle != null)
            return;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        _bannerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }
}
