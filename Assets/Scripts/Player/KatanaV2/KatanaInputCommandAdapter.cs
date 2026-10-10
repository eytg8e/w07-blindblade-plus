using UnityEngine;
using UnityEngine.InputSystem;

namespace BlindBlade.Player.Katana
{
    /// <summary>이동·시점·일반 공격·쳐내기·웅크리기 입력을 카타나 명령과 플레이어 시스템에 전달한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KatanaCombatCoordinator))]
    [DefaultExecutionOrder(-40)]
    public sealed class KatanaInputCommandAdapter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KatanaCombatCoordinator _combat;
        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnClick = true;

        private InputSystem_Actions _actions;
        private InputAction _crouchHold;
        private InputAction _crouchToggle;
        private InputAction _parry;
        private bool _crouchToggled;

        void Awake()
        {
            if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
            _actions = new InputSystem_Actions();
            _crouchHold = new InputAction("KatanaCrouchHold", InputActionType.Button);
            _crouchHold.AddBinding("<Keyboard>/leftCtrl");
            _crouchHold.AddBinding("<Keyboard>/rightCtrl");
            _crouchToggle = new InputAction("KatanaCrouchToggle", InputActionType.Button, "<Keyboard>/c");
            _parry = new InputAction("KatanaParry", InputActionType.Button, "<Mouse>/rightButton");
        }

        void OnEnable()
        {
            _actions.Player.Enable();
            _crouchHold.Enable();
            _crouchToggle.Enable();
            _parry.Enable();
        }

        void OnDisable()
        {
            _actions?.Player.Disable();
            _crouchHold?.Disable();
            _crouchToggle?.Disable();
            _parry?.Disable();
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
            _parry?.Dispose();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursor(false);
                _combat.Player.SetMovementInput(Vector2.zero);
                _combat.CancelParryAttempt();
                return;
            }
            if (_lockCursorOnClick && Cursor.lockState != CursorLockMode.Locked)
            {
                if (_actions.Player.Attack.WasPressedThisFrame() || _parry.WasPressedThisFrame())
                    SetCursor(true);
                return;
            }
            if (_combat.Player.IsPaused || !Application.isFocused) return;

            if (_crouchToggle.WasPressedThisFrame())
                _crouchToggled = !_crouchToggled;
            _combat.Player.SetCrouchInput(_crouchHold.IsPressed() || _crouchToggled);
            _combat.SetMovementInput(_actions.Player.Move.ReadValue<Vector2>());
            _combat.AddLookInput(_actions.Player.Look.ReadValue<Vector2>());
            if (_parry.WasPressedThisFrame()) _combat.RequestParry();
            if (_actions.Player.Attack.WasPressedThisFrame()) _combat.RequestNormalAttack();
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

        /// <summary>locked에 따라 커서 잠금과 표시를 함께 변경한다.</summary>
        private void SetCursor(bool locked)
        {
            if (!_lockCursorOnClick) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}