using System;

using UnityEngine;

using UnityEngine.InputSystem;

/// <summary>현재 플레이어의 실제 입력과 게임플레이 입력 허용 정책을 관리한다.</summary>
[DefaultExecutionOrder(-40)]
public sealed class InputManager : MonoBehaviour
{
    [Header("커서")]
    [SerializeField] private bool _lockCursorOnClick = true;

    [Header("패드 시점")]
    [SerializeField, Min(1f)] private float _gamepadLookDegreesPerSecond = 180f;

    [Header("입력 상태")]
    private GameStateManager _state;
    private KatanaCombatCoordinator _combat;
    private PlayerMotor _motor;
    private InputSystem_Actions _actions;
    private InputAction _crouchHold;
    private InputAction _crouchToggle;
    private InputAction _guard;
    private InputAction _dodge;
    private bool _crouchToggled;

    public bool IsInitialized => _state != null;
    public bool IsGameplayInputEnabled { get; private set; }
    public event Action<bool> GameplayInputEnabledChanged;

    void Awake()
    {
        _actions = new InputSystem_Actions();
        _crouchHold = new InputAction("CrouchHold", InputActionType.Button);
        _crouchHold.AddBinding("<Keyboard>/leftCtrl");
        _crouchHold.AddBinding("<Keyboard>/rightCtrl");
        _crouchToggle = new InputAction("CrouchToggle", InputActionType.Button, "<Keyboard>/c");
        _guard = new InputAction("Guard", InputActionType.Button, "<Mouse>/rightButton");
        _dodge = new InputAction("Dodge", InputActionType.Button, "<Keyboard>/space");
        _crouchToggle.AddBinding("<Gamepad>/buttonEast");
        _guard.AddBinding("<Gamepad>/leftShoulder");
        _dodge.AddBinding("<Gamepad>/buttonSouth");
        InputAction attack = _actions.Player.Attack;
        for (int i = 0; i < attack.bindings.Count; i++)
            if (attack.bindings[i].path.StartsWith("<Gamepad>"))
                attack.ApplyBindingOverride(i, "<Gamepad>/rightShoulder");
    }

    void Update()
    {
        InputDeviceDisplay.PollButtons();
        PlayerController player = Game.Player;
        if (!IsGameplayInputEnabled || player == null || player.IsPaused) return;
        if (InputDeviceDisplay.CancelPressed)
        {
            ClearHeldInput();
            SetCursor(false);
            return;
        }
        if (!Application.isFocused) return;
        if (_lockCursorOnClick && !InputDeviceDisplay.IsGamepad && Cursor.lockState != CursorLockMode.Locked)
        {
            if (_actions.Player.Attack.WasPressedThisFrame() || _guard.WasPressedThisFrame()) SetCursor(true);
            return;
        }
        if (_crouchToggle.WasPressedThisFrame()) _crouchToggled = !_crouchToggled;
        player.SetCrouchInput(_crouchHold.IsPressed() || _crouchToggled);
        player.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
        if (_actions.Player.Look.activeControl != null && _actions.Player.Look.activeControl.device is Gamepad)
            _motor.AddGamepadLookInput(look, _gamepadLookDegreesPerSecond, Time.deltaTime);
        else
            player.AddLookInput(look);

        // 새 우클릭은 공격을 가드로 전환하고, 계속 누르는 우클릭은 공격 완료 뒤 가드로 복귀시킨다.
        if (_guard.WasPressedThisFrame() && !player.IsGuarding)
            _combat.BeginDefense();
        else if (_guard.IsPressed() && !player.IsGuarding
            && player.CurrentState == PlayerActionState.Free)
            _combat.BeginDefense();

        if (_guard.WasReleasedThisFrame()) _combat.EndDefense();
        if (_dodge.WasPressedThisFrame()) player.RequestDodge();
        if (_actions.Player.Attack.WasPressedThisFrame()) _combat.RequestAttack();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        ClearHeldInput();
        SetCursor(false);
    }

    void OnDisable()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
        _actions.Dispose();
        _crouchHold.Dispose();
        _crouchToggle.Dispose();
        _guard.Dispose();
        _dodge.Dispose();
    }

    /// <summary>state와 Game.Player의 장치를 연결한다. 초기 입력은 차단하고 Playing 전환을 기다린다.</summary>
    public void Initialize(GameStateManager state)
    {
        Shutdown();
        _state = state;
        _combat = Game.Player.GetComponent<KatanaCombatCoordinator>();
        _motor = Game.Player.GetComponent<PlayerMotor>();
        _state.StateChanged += HandleState;
        _actions.Player.Enable();
        _crouchHold.Enable();
        _crouchToggle.Enable();
        _guard.Enable();
        _dodge.Enable();
    }

    /// <summary>state가 Playing이면 입력을 허용하고 그 외에는 명령 전달과 유지 입력을 차단한다.</summary>
    private void HandleState(GameplayState state)
    {
        SetGameplayInputEnabled(state == GameplayState.Playing);
    }

    /// <summary>allowed와 게임 상태로 입력 허용 여부를 결정한다. 차단 시 유지 입력을 정리하고 정책 변경을 알린다.</summary>
    public void SetGameplayInputEnabled(bool allowed)
    {
        bool enabled = allowed && _state != null && _state.State == GameplayState.Playing;
        if (!enabled) ClearHeldInput();
        if (IsGameplayInputEnabled == enabled) return;
        IsGameplayInputEnabled = enabled;
        GameplayInputEnabledChanged?.Invoke(enabled);
    }

    /// <summary>Game.Player의 이동·웅크리기·가드·차지·패링 시도를 정리하고 입력 토글과 모터 입력을 초기화한다.</summary>
    private void ClearHeldInput()
    {
        _crouchToggled = false;
        PlayerController player = Game.Player;
        if (player == null) return;
        player.SetMovementInput(Vector2.zero);
        player.SetCrouchInput(false);
        player.EndGuard();
        player.EndCharge();
        if (_combat != null) _combat.CancelParryAttempt();
        if (_motor != null) _motor.ClearInput();
    }

    /// <summary>locked와 Inspector 설정으로 커서 잠금·표시 상태를 변경한다. 잠금 관리가 꺼져 있으면 상태를 유지한다.</summary>
    private void SetCursor(bool locked)
    {
        if (!_lockCursorOnClick) return;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>튜토리얼 재개에 사용한 command를 복구된 플레이어에 한 번 전달한다. 회피 방향은 현재 이동 입력을 사용한다.</summary>
    public void ApplyTutorialInput(TutorialResumeInput command)
    {
        PlayerController player = Game.Player;
        if (!IsGameplayInputEnabled || player == null || player.IsPaused || !Application.isFocused) return;
        SetCursor(true);
        player.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        switch (command)
        {
            case TutorialResumeInput.Attack:
                _combat.RequestAttack();
                break;
            case TutorialResumeInput.Defense:
                _combat.BeginDefense();
                break;
            case TutorialResumeInput.Dodge:
                player.RequestDodge();
                break;
            case TutorialResumeInput.Crouch:
                player.SetCrouchInput(true);
                break;
        }
    }

    /// <summary>입력 액션과 상태 구독·플레이어 장치 참조를 해제한다. 매니저는 다음 씬에서 재사용한다.</summary>
    public void Shutdown()
    {
        SetGameplayInputEnabled(false);
        if (_state != null) _state.StateChanged -= HandleState;
        _state = null;
        _combat = null;
        _motor = null;
        if (_actions != null) _actions.Player.Disable();
        if (_crouchHold != null) _crouchHold.Disable();
        if (_crouchToggle != null) _crouchToggle.Disable();
        if (_guard != null) _guard.Disable();
        if (_dodge != null) _dodge.Disable();
        SetCursor(false);
    }
}
