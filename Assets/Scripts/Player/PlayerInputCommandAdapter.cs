using UnityEngine;

using UnityEngine.InputSystem;

/// <summary>
/// 독립 입력 액션을 플레이어 명령으로 변환하고 입력 정책에 따라 전달과 유지 입력을 제어한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInputCommandAdapter : MonoBehaviour
{
    [SerializeField] private PlayerController _player;

    [Header("Cursor")]
    [SerializeField] private bool _lockCursorOnMouseClick = true;

    private BlindBlade.Gameplay.InputManager _inputManager;
    private PlayerMotor _motor;
    private InputSystem_Actions _actions;
    private InputAction _guardAction;
    private bool _resyncAfterPause;
    private bool _inputAllowed = true;

    /// <summary>allowed에 따라 명령 전달을 허용하고 차단 시 유지 입력을 정리한다.</summary>
    public void SetInputAllowed(bool allowed)
    {
        _inputAllowed = allowed;
        if (!allowed && _player != null)
        {
            _player.SetMovementInput(Vector2.zero);
            _player.EndGuard();
            _player.EndCharge();
            _motor.ClearInput();
        }
        _resyncAfterPause = allowed;
    }


    void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();

        _motor = _player.GetComponent<PlayerMotor>();
        CreateInputActions();
    }

    void OnEnable()
    {
        if (_actions == null || _guardAction == null)
            CreateInputActions();

        if (Game.Instance.InputManager != null && Game.Instance.InputManager.IsInitialized) Initialize();
        else SetInputAllowed(true);
        _actions.Player.Enable();
        _guardAction.started += OnGuardStarted;
        _guardAction.canceled += OnGuardCanceled;
        _guardAction.Enable();

        _actions.Player.Move.performed += OnMove;
        _actions.Player.Move.canceled += OnMove;
        _actions.Player.Look.performed += OnLook;
        _actions.Player.Attack.performed += OnAttack;
        _actions.Player.Interact.started += OnChargeStarted;
        _actions.Player.Interact.canceled += OnChargeCanceled;
    }

    void OnDisable()
    {
        Shutdown();
        if (_actions != null)
        {
            _actions.Player.Move.performed -= OnMove;
            _actions.Player.Move.canceled -= OnMove;
            _actions.Player.Look.performed -= OnLook;
            _actions.Player.Attack.performed -= OnAttack;
            _actions.Player.Interact.started -= OnChargeStarted;
            _actions.Player.Interact.canceled -= OnChargeCanceled;
            _actions.Player.Disable();
        }

        if (_guardAction != null)
        {
            _guardAction.started -= OnGuardStarted;
            _guardAction.canceled -= OnGuardCanceled;
            _guardAction.Disable();
        }

        if (_inputAllowed && _player != null && !_player.IsPaused)
        {
            _player.SetMovementInput(Vector2.zero);
            _player.EndGuard();
            _player.EndCharge();
        }

        UnlockCursor();
    }

private void OnDestroy()
    {
        _actions?.Dispose();
        _guardAction?.Dispose();
    }

private void Update()
    {
        if (!_inputAllowed) return;
        if (_lockCursorOnMouseClick
            && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
            return;
        }

        if (_lockCursorOnMouseClick && Cursor.lockState != CursorLockMode.Locked)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null
                && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                LockCursor();
            }
        }

        if (_player == null)
            return;

        if (_player.IsPaused)
        {
            _resyncAfterPause = true;
            return;
        }

        if (!_resyncAfterPause)
            return;

        _resyncAfterPause = false;
        _player.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        SynchronizeHeldAction(_guardAction != null && _guardAction.IsPressed(), _player.IsGuarding, _player.BeginGuard, _player.EndGuard);
        SynchronizeHeldAction(_actions.Player.Interact.IsPressed(), _player.IsCharging, _player.BeginCharge, _player.EndCharge);
    }

    /// <summary>Game에 등록된 입력 매니저를 구독하고 현재 입력 허용 상태를 적용한다.</summary>
    public void Initialize()
    {
        Shutdown();
        _inputManager = Game.Instance.InputManager;
        _inputManager.GameplayInputEnabledChanged += SetInputAllowed;
        SetInputAllowed(_inputManager.IsGameplayInputEnabled);
    }

    /// <summary>입력을 차단하고 이전 입력 정책 구독과 매니저 참조를 해제한다.</summary>
    public void Shutdown()
    {
        SetInputAllowed(false);
        if (_inputManager != null) _inputManager.GameplayInputEnabledChanged -= SetInputAllowed;
        _inputManager = null;
    }

    /// <summary>입력 콜백의 Vector2 값을 플레이어 이동 명령으로 전달합니다.</summary>
    private void OnMove(InputAction.CallbackContext context)
    {
        if (_inputAllowed && _player != null && !_player.IsPaused)
            _player.SetMovementInput(context.ReadValue<Vector2>());
    }

    /// <summary>입력 콜백의 Vector2 변화량을 플레이어 시점 명령으로 전달합니다.</summary>
    private void OnLook(InputAction.CallbackContext context)
    {
        if (_inputAllowed && _player != null && !_player.IsPaused)
            _player.AddLookInput(context.ReadValue<Vector2>());
    }

    /// <summary>공격 입력을 현재 상태에 따라 일반 공격 또는 준비된 발도 공격으로 전달합니다.</summary>
private void OnAttack(InputAction.CallbackContext context)
    {
        if (!_inputAllowed || _player == null || _player.IsPaused)
            return;

        if (_lockCursorOnMouseClick && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
            return;
        }

        if (_player.CurrentState == PlayerActionState.Free)
            _player.RequestNormalAttack();
        else if (_player.IsChargingReady)
            _player.RequestIaidoAttack();
    }

    /// <summary>기 모으기 입력 시작을 플레이어 명령으로 전달합니다.</summary>
    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (_inputAllowed && _player != null && !_player.IsPaused)
            _player.BeginCharge();
    }

    /// <summary>기 모으기 입력 종료를 플레이어 명령으로 전달합니다.</summary>
    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (_inputAllowed && _player != null && !_player.IsPaused)
            _player.EndCharge();
    }

    /// <summary>우클릭 입력 시작을 가드 시작 명령으로 전달합니다.</summary>


    /// <summary>우클릭 입력 종료를 가드 종료 명령으로 전달합니다.</summary>


private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            UnlockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    /// <summary>일시정지 후 눌림 입력과 플레이어의 유지 행동을 비교해 시작 또는 종료 콜백을 호출합니다.</summary>
    private static void SynchronizeHeldAction(bool isPressed, bool isActive, System.Func<bool> begin, System.Action end)
    {
        if (isPressed && !isActive)
            begin();
        else if (!isPressed && isActive)
            end();
    }


private void CreateInputActions()
    {
        _actions = new InputSystem_Actions();
        _guardAction = new InputAction("Guard", InputActionType.Button, "<Mouse>/rightButton");

        InputAction chargeAction = _actions.Player.Interact;
        const string existingKeyboardBinding = "<Keyboard>/e";
        const string chargeControlBinding = "<Keyboard>/leftCtrl";

        // Override only the keyboard binding on this adapter's private action instance.
        // Other users of the shared Input Actions asset keep their original bindings.
        for (int i = 0; i < chargeAction.bindings.Count; i++)
        {
            if (chargeAction.bindings[i].path != existingKeyboardBinding)
                continue;

            chargeAction.ApplyBindingOverride(i, chargeControlBinding);
            return;
        }

        Debug.LogWarning($"Could not find the expected charge binding '{existingKeyboardBinding}'. Charge input remains unchanged.", this);
    }





private void OnGuardStarted(InputAction.CallbackContext context)
    {
        if (!_inputAllowed || _player == null || _player.IsPaused)
            return;

        if (_lockCursorOnMouseClick)
            LockCursor();

        _player.BeginGuard();
    }

    private void OnGuardCanceled(InputAction.CallbackContext context)
    {
        if (_inputAllowed && _player != null && !_player.IsPaused)
            _player.EndGuard();
    }
}
