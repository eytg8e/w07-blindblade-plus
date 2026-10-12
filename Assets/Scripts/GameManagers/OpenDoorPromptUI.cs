using UnityEngine;

using TMPro;

/// <summary>문 상호작용 안내의 문구와 표시 대상을 관리한다.</summary>
public sealed class OpenDoorPromptUI : GameUI
{
    [Header("문 안내")]
    [SerializeField] private TMP_Text _text;
    private UnityEngine.Object _owner;

    /// <summary>이전 표시 대상을 정리하고 문 안내를 숨긴다.</summary>
    public override void Initialize()
    {
        Shutdown();
    }

    /// <summary>owner의 unlocked 상태와 prompt를 사용해 상호작용 안내 문구와 표시 대상을 갱신한다.</summary>
    public void ShowFor(UnityEngine.Object owner, bool unlocked, string prompt = "[F] OPEN DOOR")
    {
        string message = unlocked ? InputDeviceDisplay.FormatKeys(prompt) : "DEFEAT ALL ENEMIES";
        if (_text.text != message)
            _text.text = message;
        _owner = owner;
        if (!gameObject.activeSelf) SetVisible(true);
    }

    /// <summary>owner가 현재 안내 대상이면 화면을 숨기고 대상을 해제한다.</summary>
    public void HideFor(UnityEngine.Object owner)
    {
        if (_owner != owner) return;
        Shutdown();
    }

    /// <summary>현재 안내 대상을 해제하고 화면을 숨긴다.</summary>
    public override void Shutdown()
    {
        _owner = null;
        SetVisible(false);
    }
}
