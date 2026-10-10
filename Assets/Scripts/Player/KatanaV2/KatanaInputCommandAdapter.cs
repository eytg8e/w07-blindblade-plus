using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>입력 정책에 따라 카타나 명령 전달과 유지 입력 정리를 담당한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KatanaCombatCoordinator))]
[DefaultExecutionOrder(-40)]
public sealed class KatanaInputCommandAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KatanaCombatCoordinator _combat;
    [Header("Cursor")]
    [SerializeField] private bool _lockCursorOnClick = true;

    private InputManager _inputManager;
    private PlayerInputCommandAdapter _legacyAdapter;
    private PlayerMotor _motor;
    private InputSystem_Actions _actions;

    private InputAction _crouchHold;
    private InputAction _crouchToggle;
    private bool _crouchToggled;
    private InputAction _charge;
    private InputAction _parry;
    private bool _inputAllowed = true;

    /// <summary>allowed에 따라 카타나 명령 전달을 허용하고 차단 시 유지 행동을 정리한다.</summary>
    public void SetInputAllowed(bool allowed)
    {
        _inputAllowed = allowed && (_legacyAdapter == null || !_legacyAdapter.enabled);
        if (!_inputAllowed && _combat != null)
        {
            _combat.SetMovementInput(Vector2.zero);
            _combat.CancelParryAttempt();
            _combat.EndCharge();
            _combat.Player.EndGuard();
            _motor.ClearInput();
        }
    }
    
    void Awake()
    {
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        _legacyAdapter = GetComponent<PlayerInputCommandAdapter>();
        _motor = _combat.Player.GetComponent<PlayerMotor>();
        _actions = new InputSystem_Actions();
        _crouchHold = new InputAction("KatanaCrouchHold", InputActionType.Button);
        _crouchHold.AddBinding("<Keyboard>/leftCtrl");
        _crouchHold.AddBinding("<Keyboard>/rightCtrl");
        _crouchToggle = new InputAction("KatanaCrouchToggle", InputActionType.Button, "<Keyboard>/c");
    }

    void OnEnable()
    {
        if (Game.Instance.InputManager != null && Game.Instance.InputManager.IsInitialized) Initialize();
        else SetInputAllowed(true);
        _actions.Player.Enable();
        _crouchHold.Enable();
        _crouchToggle.Enable();
    }

    void OnDisable()
    {
        Shutdown();
        _actions?.Player.Disable();
        _crouchHold?.Disable();
        _crouchToggle?.Disable();
        _crouchToggled = false;
        if (_combat != null)
        {
            _combat.Player.SetMovementInput(Vector2.zero);
            _combat.Player.SetCrouchInput(false);
            _combat.CancelParryAttempt();
        }
        SetCursor(false);
    }

    void OnDestroy()
    {
        _actions?.Dispose();
        _crouchHold?.Dispose();
        _crouchToggle?.Dispose();
    }

    void Update()
    {
        if (!_inputAllowed) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursor(false);
            _combat.Player.SetMovementInput(Vector2.zero);
            _combat.CancelParryAttempt();
            return;
        }
        if (_lockCursorOnClick && Cursor.lockState != CursorLockMode.Locked)
        {
            if (_actions.Player.Attack.WasPressedThisFrame())
                SetCursor(true);
            return;
        }
        if (_combat.Player.IsPaused || !Application.isFocused) return;

        if (_crouchToggle.WasPressedThisFrame())
            _crouchToggled = !_crouchToggled;
        _combat.Player.SetCrouchInput(_crouchHold.IsPressed() || _crouchToggled);
        _combat.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
        _combat.AddLookInput(_actions.Player.Look.ReadValue<Vector2>());
        if (_actions.Player.Attack.WasPressedThisFrame()) _combat.RequestAttack();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused || _combat == null) return;
        SetCursor(false);
        _crouchToggled = false;
        _combat.Player.SetMovementInput(Vector2.zero);
        _combat.Player.SetCrouchInput(false);
        _combat.CancelParryAttempt();
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

    /// <summary>locked에 따라 커서 잠금과 표시를 함께 변경한다.</summary>
    private void SetCursor(bool locked)
    {
        if (!_lockCursorOnClick) return;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
