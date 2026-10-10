using UnityEngine;

using UnityEngine.InputSystem;

namespace BlindBlade.Player.Katana
{
    /// <summary>입력을 카타나 명령 창구에 전달한다. 원본 입력 에셋과 기존 입력 어댑터는 변경하지 않는다.</summary>
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
        private InputAction _charge;
        private InputAction _parry;

        void Awake()
        {
            if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
            _actions = new InputSystem_Actions();
            _charge = new InputAction("KatanaCharge", InputActionType.Button, "<Keyboard>/leftCtrl");
            _parry = new InputAction("KatanaParry", InputActionType.Button, "<Mouse>/rightButton");
        }

        void OnEnable()
        {
            _actions.Player.Enable();
            _charge.Enable();
            _parry.Enable();
        }

        void OnDisable()
        {
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

        /// <summary>locked에 따라 커서 잠금과 표시를 함께 변경한다.</summary>
        private void SetCursor(bool locked)
        {
            if (!_lockCursorOnClick) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
