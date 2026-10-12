using UnityEngine;

/// <summary>Moves one or two assigned shoji leaves through the shared interaction contract.</summary>
public sealed class JapaneseSlideDoor : MonoBehaviour, IInteractable
{
    [Header("Door Leaves")]
    [SerializeField] private Transform _leftLeaf;
    [SerializeField] private Transform _rightLeaf;
    [SerializeField] private Vector3 _leftClosedPosition;
    [SerializeField] private Vector3 _rightClosedPosition;

    [Header("Sliding")]
    [SerializeField] private Vector3 _slideDirection = Vector3.right;
    [SerializeField, Min(0f)] private float _slideDistance = 1f;
    [SerializeField, Min(0.01f)] private float _duration = 0.65f;
    private float _progress;
    private bool _targetOpen;
    public bool IsOpen => _progress >= 1f;
    public bool CanInteract => isActiveAndEnabled;
    public string InteractionPrompt => _targetOpen ? "[F] CLOSE DOOR" : "[F] OPEN DOOR";

    /// <summary>Reverses the requested sliding state without input parameters through the shared interaction contract.</summary>
    public void Interact()
    {
        Toggle();
    }

    void Awake()
    {
        ApplyPose(0f);
    }

    void Update()
    {
        float target = _targetOpen ? 1f : 0f;
        if (_progress == target) return;
        _progress = Mathf.MoveTowards(_progress, target, Time.deltaTime / Mathf.Max(0.01f, _duration));
        ApplyPose(_progress);
    }

    /// <summary>Requests opening both leaves; in edit mode applies the open preview immediately.</summary>
    [ContextMenu("Open")]
    public void Open()
    {
        SetOpen(true);
    }

    /// <summary>Requests closing both leaves; in edit mode restores the configured closed positions.</summary>
    [ContextMenu("Close")]
    public void Close()
    {
        SetOpen(false);
    }

    /// <summary>Reverses the requested open state while preserving the current sliding progress.</summary>
    [ContextMenu("Toggle")]
    public void Toggle()
    {
        SetOpen(!_targetOpen);
    }

    /// <summary>Uses open to choose the target state; edit mode applies the corresponding endpoint pose.</summary>
    public void SetOpen(bool open)
    {
        _targetOpen = open;
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Preview Shoji Door");
            if (_leftLeaf != null) UnityEditor.Undo.RecordObject(_leftLeaf, "Preview Shoji Door");
            if (_rightLeaf != null) UnityEditor.Undo.RecordObject(_rightLeaf, "Preview Shoji Door");
#endif
            _progress = open ? 1f : 0f;
            ApplyPose(_progress);
        }
    }

    /// <summary>Uses normalized progress and configured travel to update both leaf positions in parent space.</summary>
    private void ApplyPose(float progress)
    {
        Vector3 offset = _slideDirection.normalized * (_slideDistance * Mathf.SmoothStep(0f, 1f, progress));
        if (_leftLeaf != null) _leftLeaf.localPosition = _leftClosedPosition - offset;
        if (_rightLeaf != null) _rightLeaf.localPosition = _rightClosedPosition + offset;
    }
}
