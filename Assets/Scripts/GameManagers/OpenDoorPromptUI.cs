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
        InputDeviceDisplay.PollButtons();
        string message = unlocked ? FormatDoorPrompt(prompt) : "DEFEAT ALL ENEMIES";
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

    /// <summary>문 열기·닫기 문구를 현재 입력 장치의 실제 상호작용 키 F 또는 X로 표시한다. 다른 안내 문구는 기존 변환을 유지한다.</summary>
    private static string FormatDoorPrompt(string prompt)
    {
        string action = null;
        if (!string.IsNullOrEmpty(prompt))
        {
            if (prompt.IndexOf("CLOSE DOOR", System.StringComparison.OrdinalIgnoreCase) >= 0 || prompt.Contains("문 닫기"))
                action = "문 닫기";
            else if (prompt.IndexOf("OPEN DOOR", System.StringComparison.OrdinalIgnoreCase) >= 0 || prompt.Contains("문 열기"))
                action = "문 열기";
        }
        return action == null ? InputDeviceDisplay.FormatKeys(prompt)
            : InputDeviceDisplay.FormatKeys(action + " - (F)", action + " - (X)");
    }
}
