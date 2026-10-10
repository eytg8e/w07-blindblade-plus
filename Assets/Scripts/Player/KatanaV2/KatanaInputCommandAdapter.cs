using UnityEngine;

using UnityEngine.InputSystem;

namespace BlindBlade.Player.Katana
{
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

        private BlindBlade.Gameplay.InputManager _inputManager;
        private PlayerInputCommandAdapter _legacyAdapter;
        private PlayerMotor _motor;
        private InputSystem_Actions _actions;
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
            _charge = new InputAction("KatanaCharge", InputActionType.Button, "<Keyboard>/leftCtrl");
            _parry = new InputAction("KatanaParry", InputActionType.Button, "<Mouse>/rightButton");
        }

        void OnEnable()
        {
            if (Game.Instance.InputManager != null && Game.Instance.InputManager.IsInitialized) Initialize();
            else SetInputAllowed(true);
            _actions.Player.Enable();
            _charge.Enable();
            _parry.Enable();
        }

        void OnDisable()
        {
            Shutdown();
            _actions?.Player.Disable();
            _charge?.Disable();
            _parry?.Disable();
            if (_combat != null)
            {
                _combat.SetMovementInput(Vector2.zero);
                _combat.CancelParryAttempt();
                if (!_combat.Player.IsPaused) _combat.EndCharge();
            }
            SetCursor(false);
        }

        void OnDestroy()
        {
            _actions?.Dispose();
            _charge?.Dispose();
            _parry?.Dispose();
        }

        void Update()
        {
            if (!_inputAllowed) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursor(false);
                _combat.SetMovementInput(Vector2.zero);
                _combat.CancelParryAttempt();
                if (!_combat.Player.IsPaused) _combat.EndCharge();
                return;
            }
            if (_lockCursorOnClick && Cursor.lockState != CursorLockMode.Locked)
            {
                if (_actions.Player.Attack.WasPressedThisFrame() || _parry.WasPressedThisFrame())
                    SetCursor(true);
                return;
            }
            if (_combat.Player.IsPaused || !Application.isFocused) return;
            _combat.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
            _combat.AddLookInput(_actions.Player.Look.ReadValue<Vector2>());
            if (_combat.Player.IsCharging && !_charge.IsPressed()) _combat.EndCharge();
            if (_charge.WasPressedThisFrame()) _combat.BeginCharge();
            if (_parry.WasPressedThisFrame()) _combat.RequestParry();
            if (_actions.Player.Attack.WasPressedThisFrame())
            {
                if (_combat.Player.IsChargingReady) _combat.RequestIaidoAttack();
                else _combat.RequestNormalAttack();
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused || _combat == null) return;
            SetCursor(false);
            _combat.SetMovementInput(Vector2.zero);
            _combat.CancelParryAttempt();
            if (!_combat.Player.IsPaused) _combat.EndCharge();
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
}
