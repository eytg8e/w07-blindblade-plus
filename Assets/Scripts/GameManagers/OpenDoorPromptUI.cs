using UnityEngine;

using TMPro;

/// <summary>문 상호작용 안내의 문구와 표시 대상을 관리한다.</summary>
public sealed class OpenDoorPromptUI : GameUI
{
    [Header("문 안내")]
    [SerializeField] private TMP_Text _text;
    private StageDoor _owner;
    private bool _unlocked;

    /// <summary>이전 표시 대상을 정리하고 문 안내를 숨긴다.</summary>
    public override void Initialize()
    {
        Shutdown();
    }

    /// <summary>door의 unlocked 상태에 맞는 안내 문구를 표시한다.</summary>
    public void ShowFor(StageDoor door, bool unlocked)
    {
        if (_owner != door || _unlocked != unlocked)
        {
            _text.text = unlocked ? "[F] OPEN DOOR" : "DEFEAT ALL ENEMIES";
            _unlocked = unlocked;
        }
        _owner = door;
        if (!gameObject.activeSelf) SetVisible(true);
    }

    /// <summary>door가 현재 안내 대상이면 화면을 숨기고 대상을 해제한다.</summary>
    public void HideFor(StageDoor door)
    {
        if (_owner != door) return;
        Shutdown();
    }

    /// <summary>현재 안내 대상을 해제하고 화면을 숨긴다.</summary>
    public override void Shutdown()
    {
        _owner = null;
        SetVisible(false);
    }
}
