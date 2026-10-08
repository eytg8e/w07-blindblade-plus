using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test-only incoming attack probe for PlayerDebugSandbox.
/// It asks the player guard contract to block a synthetic hit and never applies damage.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerGuardDebugInput : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private bool _showHint = true;

    private string _statusText = "F: incoming attack probe  |  Hold RMB to guard";

    private void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (_player == null || keyboard == null || !keyboard.fKey.wasPressedThisFrame || _player.IsPaused)
            return;

        if (_player.TryBlockIncomingAttack())
        {
            _player.NotifyGuardSuccess();
            _statusText = "BLOCKED  |  synthetic attack";
            Debug.Log("[Guard Probe] Synthetic incoming attack was blocked.", this);
        }
        else
        {
            _statusText = "NOT BLOCKING  |  synthetic attack";
            Debug.Log("[Guard Probe] No guard was active; no damage system is connected in this sandbox.", this);
        }
    }

    private void OnGUI()
    {
        if (!_showHint)
            return;

        GUI.Box(new Rect(12f, Screen.height - 48f, 430f, 34f), _statusText);
    }
}
