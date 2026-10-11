using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public sealed class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Camera _viewCamera;
    [SerializeField, Min(0.1f)] private float _range = 3f;
    [SerializeField] private LayerMask _raycastMask = ~0;
    private PlayerController _player;
    private OpenDoorPromptUI _promptUI;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    void Start()
    {
        _promptUI = Game.Instance.UIManager.Get<OpenDoorPromptUI>();
    }

    void Update()
    {
        if (_player.IsPaused ||
            _player.CurrentState == PlayerActionState.Dead)
        {
            _promptUI.HideFor(this);
            return;
        }

        IInteractable target = FindTarget();

        if (target == null)
        {
            _promptUI.HideFor(this);
            return;
        }

        _promptUI.ShowFor(
            this, target.CanInteract, target.InteractionPrompt);

        if (target.CanInteract &&
            Keyboard.current != null &&
            Keyboard.current.fKey.wasPressedThisFrame)
        {
            target.Interact();
        }
    }

    void OnDisable()
    {
        if (_promptUI != null)
            _promptUI.HideFor(this);
    }

    /// <summary>
    /// Uses the camera, range, and mask to return the first visible
    /// interactable, or null when the ray hits another object.
    /// </summary>
    private IInteractable FindTarget()
    {
        Ray ray = _viewCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(
            ray, out RaycastHit hit, _range,
            _raycastMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<IInteractable>();
    }
}
