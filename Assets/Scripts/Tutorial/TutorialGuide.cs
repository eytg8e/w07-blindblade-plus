using System;

using UnityEngine;

using TMPro;

public enum TutorialObjective
{
    Read, Move, Look, Crouch, Dodge, Attack, Combo, Guard, Parry, GuardAttack, AttackGuard, TargetedAttack, Door
}

/// <summary>한 안내 구간의 독백, 키 표시 및 확인할 행동을 보관한다.</summary>
[Serializable]
public sealed class TutorialLesson
{
    [Header("안내 문구")]
    [SerializeField] private string _name;
    [SerializeField] private string _keys;
    [SerializeField, TextArea(2, 3)] private string[] _monologue;
    [SerializeField] private TutorialObjective _objective;

    [Header("씬 연결")]
    [SerializeField] private Transform _anchor;
    [SerializeField] private BoxCollider _area;
    [SerializeField] private TMP_Text _keyLabel;
    [SerializeField] private LineRenderer[] _rings;
    [SerializeField] private JapaneseSlideDoor _door;
    private bool _completed;

    public string Name => _name;
    public string Keys => _keys;
    public string[] Monologue => _monologue;
    public TutorialObjective Objective => _objective;
    public Transform Anchor => _anchor;
    public TMP_Text KeyLabel => _keyLabel;
    public LineRenderer[] Rings => _rings;
    public JapaneseSlideDoor Door => _door;
    public bool Completed => _completed;

    /// <summary>문구, 목표 및 씬 참조로 편집 가능한 안내 데이터를 생성한다.</summary>
    public TutorialLesson(string name, string keys, string[] monologue, TutorialObjective objective,
        Transform anchor, BoxCollider area, TMP_Text keyLabel, LineRenderer[] rings, JapaneseSlideDoor door = null)
    {
        _name = name;
        _keys = keys;
        _monologue = monologue;
        _objective = objective;
        _anchor = anchor;
        _area = area;
        _keyLabel = keyLabel;
        _rings = rings;
        _door = door;
    }

    /// <summary>worldPosition이 안내 영역 내부이면 true를 반환한다. 기울어진 영역도 로컬 좌표로 검사한다.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        if (!_area.gameObject.activeInHierarchy) return false;
        Vector3 point = _area.transform.InverseTransformPoint(worldPosition) - _area.center;
        Vector3 half = _area.size * 0.5f;
        return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
    }

    /// <summary>실제 행동이 확인된 안내의 완료 상태를 기록한다. 통행이나 전투는 제한하지 않는다.</summary>
    public void Complete()
    {
        _completed = true;
    }
}

/// <summary>튜토리얼 씬에서 기존 독백 UI와 바닥 키 홀로그램을 연결하고 실제 행동을 관찰한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class TutorialGuide : MonoBehaviour
{
    [Header("씬 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private MonologueUI _monologueUI;
    [SerializeField] private TutorialLesson[] _lessons = Array.Empty<TutorialLesson>();

    [Header("독백")]
    [SerializeField, Min(1f)] private float _lineDuration = 6f;
    [SerializeField, Min(0f)] private float _revisitInterval = 8f;
    [SerializeField] private bool _hideScreenDoorKeys = true;
    private TutorialLesson _areaLesson;
    private TutorialLesson _hintLesson;
    private int _lineIndex;
    private float _nextLineTime;
    private float[] _lastVisited;
    private bool _ownsMonologue;

    [Header("홀로그램")]
    [SerializeField, Min(1f)] private float _visibilityDistance = 6f;
    [SerializeField, Min(0f)] private float _floatAmplitude = 0.045f;
    [SerializeField, Min(0f)] private float _floatSpeed = 1.6f;
    [SerializeField, Min(0f)] private float _keyHeight = 1.05f;
    [SerializeField] private Color _keyColor = new Color(0.3f, 0.9f, 1f);
    [SerializeField] private Color _completedColor = new Color(0.4f, 1f, 0.6f);
    [SerializeField] private Material _ringMaterial;
    [SerializeField] private Material _completedRingMaterial;
    private PlayerMotor _motor;
    private KatanaCombatCoordinator _combat;
    private Camera _camera;
    private OpenDoorPromptUI _doorPrompt;
    private Quaternion _initialView;
    private float _lastGuardEnd = float.NegativeInfinity;
    private float _lastAttack = float.NegativeInfinity;
    private bool _bound;

    public TutorialLesson[] Lessons => _lessons;

    void Start()
    {
        _lastVisited = new float[_lessons.Length];
        for (int i = 0; i < _lastVisited.Length; i++) _lastVisited[i] = float.NegativeInfinity;
        foreach (TutorialLesson lesson in _lessons)
        {
            lesson.KeyLabel.text = lesson.Keys;
            lesson.KeyLabel.color = _keyColor;
            foreach (LineRenderer ring in lesson.Rings) ring.sharedMaterial = _ringMaterial;
        }
    }

    void Update()
    {
        if (!_bound && !TryBind()) return;
        if (_player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
        {
            HideMonologue();
            return;
        }

        UpdateHolograms();
        ObserveMovement();
        TutorialLesson area = FindLesson(_player.transform.position + Vector3.up * 0.5f);
        if (area != _areaLesson)
        {
            _areaLesson = area;
            if (area != null)
            {
                int index = Array.IndexOf(_lessons, area);
                if (Time.time - _lastVisited[index] >= _revisitInterval)
                {
                    _lastVisited[index] = Time.time;
                    _hintLesson = area;
                    _lineIndex = 0;
                    ShowLine();
                }
            }
        }

        if (_ownsMonologue && Time.time >= _nextLineTime)
        {
            _lineIndex++;
            if (_lineIndex < _hintLesson.Monologue.Length) ShowLine();
            else
            {
                if (_hintLesson.Objective == TutorialObjective.Read) CompleteLesson(_hintLesson);
                HideMonologue();
            }
        }
    }

    void LateUpdate()
    {
        if (_bound && _hideScreenDoorKeys && _doorPrompt.gameObject.activeSelf)
            _doorPrompt.SetVisible(false);
    }

    void OnDisable()
    {
        if (_bound)
        {
            _player.ActionEvent -= HandleAction;
            _combat.EventRaised -= HandleCombat;
            _bound = false;
        }
        HideMonologue();
    }

    /// <summary>player, UI 및 각 구간과 홀로그램 재료를 연결한다. 씬 제작 시 사용하는 설정 메서드다.</summary>
    public void Configure(PlayerController player, MonologueUI monologue, TutorialLesson[] lessons,
        Material ringMaterial, Material completedMaterial)
    {
        _player = player;
        _monologueUI = monologue;
        _lessons = lessons;
        _ringMaterial = ringMaterial;
        _completedRingMaterial = completedMaterial;
    }

    /// <summary>게임 세션 준비 후 플레이어 표현 참조와 이벤트를 연결하고 성공 여부를 반환한다.</summary>
    private bool TryBind()
    {
        if (!Game.IsInitialized || Game.Instance.GameStateManager == null
            || Game.Instance.GameStateManager.State != GameplayState.Playing) return false;
        _motor = _player.GetComponent<PlayerMotor>();
        _combat = _player.GetComponent<KatanaCombatCoordinator>();
        _camera = _player.GetComponentInChildren<Camera>();
        _doorPrompt = Game.Instance.UIManager.Get<OpenDoorPromptUI>();
        _initialView = _camera.transform.rotation;
        _player.ActionEvent += HandleAction;
        _combat.EventRaised += HandleCombat;
        _bound = true;
        return true;
    }

    /// <summary>position을 포함하는 독백 영역 중 가장 가까운 안내를 반환한다. 문구가 없는 키 표시와 영역 밖은 null을 반환한다.</summary>
    private TutorialLesson FindLesson(Vector3 position)
    {
        TutorialLesson result = null;
        float nearest = float.PositiveInfinity;
        foreach (TutorialLesson lesson in _lessons)
        {
            if (lesson.Monologue.Length == 0 || !lesson.Contains(position)) continue;
            float distance = (lesson.Anchor.position - position).sqrMagnitude;
            if (distance < nearest) { nearest = distance; result = lesson; }
        }
        return result;
    }

    /// <summary>현재 독백 페이지를 기존 UI에 표시하고 다음 페이지 시각을 설정한다. 입력과 게임 시간을 변경하지 않는다.</summary>
    private void ShowLine()
    {
        if (_hintLesson.Monologue.Length == 0) return;
        _monologueUI.Show(_hintLesson.Monologue[_lineIndex], _lineIndex == _hintLesson.Monologue.Length - 1);
        _ownsMonologue = true;
        _nextLineTime = Time.time + _lineDuration;
    }

    /// <summary>이 안내가 표시한 독백을 숨기고 표시 소유 상태를 해제한다.</summary>
    private void HideMonologue()
    {
        if (!_ownsMonologue) return;
        if (_monologueUI != null) _monologueUI.SetVisible(false);
        _ownsMonologue = false;
    }

    /// <summary>거리로 표시 여부를 조절하고 키만 카메라를 향해 작게 부유시킨다. 물리 충돌은 생성하지 않는다.</summary>
    private void UpdateHolograms()
    {
        for (int i = 0; i < _lessons.Length; i++)
        {
            TutorialLesson lesson = _lessons[i];
            bool visible = (lesson.Anchor.position - _player.transform.position).sqrMagnitude
                <= _visibilityDistance * _visibilityDistance;
            if (lesson.KeyLabel.gameObject.activeSelf != visible) lesson.KeyLabel.gameObject.SetActive(visible);
            foreach (LineRenderer ring in lesson.Rings) ring.enabled = visible;
            if (!visible) continue;
            Vector3 local = Vector3.up * (_keyHeight + Mathf.Sin(Time.time * _floatSpeed + i) * _floatAmplitude);
            lesson.KeyLabel.transform.localPosition = local;
            Vector3 toCamera = _camera.transform.position - lesson.KeyLabel.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
                lesson.KeyLabel.transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
        }
    }

    /// <summary>실제 이동·시점·웅크리기·회피 및 연결된 문 상태를 관찰해 안내 확인 표시를 갱신한다.</summary>
    private void ObserveMovement()
    {
        if (_motor.HorizontalSpeed > 0.1f) CompleteObjective(TutorialObjective.Move);
        if (Quaternion.Angle(_initialView, _camera.transform.rotation) > 8f) CompleteObjective(TutorialObjective.Look);
        if (_motor.IsCrouching) CompleteObjective(TutorialObjective.Crouch);
        if (_motor.IsDodging) CompleteObjective(TutorialObjective.Dodge);
        foreach (TutorialLesson lesson in _lessons)
            if (lesson.Objective == TutorialObjective.Door && lesson.Door != null && lesson.Door.IsOpen)
                CompleteLesson(lesson);
    }

    /// <summary>실제 플레이어 action의 공격 시작 및 대상 도약 사건을 관찰한다. 피해와 전투 상태는 변경하지 않는다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if (action.Stage == PlayerActionStage.AttackStarted || action.Stage == PlayerActionStage.DashStarted)
        {
            _lastAttack = Time.time;
            CompleteObjective(TutorialObjective.Attack);
            if (Time.time - _lastGuardEnd < 0.5f) CompleteObjective(TutorialObjective.GuardAttack);
        }
        if (action.Stage == PlayerActionStage.DashStarted)
        {
            CompleteObjective(TutorialObjective.TargetedAttack);
            if (_player.TargetedComboStrike == 2) CompleteObjective(TutorialObjective.Combo);
        }
    }

    /// <summary>combat 사건의 가드·패링·두 번째 베기를 확인 표시로 전달한다. 성공 판정을 자체 생성하지 않는다.</summary>
    private void HandleCombat(KatanaEvent combat)
    {
        if (combat.Stage == KatanaStage.GuardStarted)
        {
            CompleteObjective(TutorialObjective.Guard);
            if (Time.time - _lastAttack < 1.5f) CompleteObjective(TutorialObjective.AttackGuard);
        }
        if (combat.Stage == KatanaStage.GuardEnded) _lastGuardEnd = Time.time;
        if (combat.Stage == KatanaStage.ParrySucceeded) CompleteObjective(TutorialObjective.Parry);
        if (combat.Stage == KatanaStage.NormalStarted && combat.Strike == 2)
            CompleteObjective(TutorialObjective.Combo);
    }

    /// <summary>objective와 일치하는 모든 안내에 실제 행동 확인을 표시한다.</summary>
    private void CompleteObjective(TutorialObjective objective)
    {
        foreach (TutorialLesson lesson in _lessons)
            if (lesson.Objective == objective) CompleteLesson(lesson);
    }

    /// <summary>lesson의 첫 완료 시 설정된 키 색상과 링 재료를 적용한다. 연습 동작과 통행은 제한하지 않는다.</summary>
    private void CompleteLesson(TutorialLesson lesson)
    {
        if (lesson.Completed) return;
        lesson.Complete();
        lesson.KeyLabel.color = _completedColor;
        foreach (LineRenderer ring in lesson.Rings) ring.sharedMaterial = _completedRingMaterial;
    }
}
