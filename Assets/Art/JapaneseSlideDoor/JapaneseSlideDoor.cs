using UnityEngine;

/// <summary>Moves the two shoji leaves without depending on player input or scene managers.</summary>
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

    /// <summary>open으로 개폐 목표를 변경하고 Play 중 목표가 바뀌면 문 위치에서 해당 효과음을 한 번 재생한다. 편집 모드에서는 끝 자세만 적용한다.</summary>
    public void SetOpen(bool open)
    {
        bool changed = _targetOpen != open;
        _targetOpen = open;
        if (Application.isPlaying && changed)
            Managers.Sound.Play(open ? SoundId.DoorOpen : SoundId.DoorClose,
                transform.position, owner: gameObject, playDuringMovement: true);
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObjects(new Object[] { this, _leftLeaf, _rightLeaf }, "Preview Shoji Door");
#endif
            _progress = open ? 1f : 0f;
            ApplyPose(_progress);
        }
    }

    /// <summary>Uses normalized progress and configured travel to update both leaf positions in parent space.</summary>
    private void ApplyPose(float progress)
    {
        Vector3 offset = _slideDirection.normalized * (_slideDistance * Mathf.SmoothStep(0f, 1f, progress));
        _leftLeaf.localPosition = _leftClosedPosition - offset;
        _rightLeaf.localPosition = _rightClosedPosition + offset;
    }
}
