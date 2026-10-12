using UnityEngine;

using TMPro;

/// <summary>공통 Canvas에서 협업자가 전달한 독백 문구와 화면 표시를 관리한다.</summary>
public sealed class MonologueUI : GameUI
{
    [Header("독백 본문")]
    [SerializeField] private TMP_Text _text;
    [SerializeField] private MonologueIndicator _indicator;

    /// <summary>이전 문구를 비우고 화면을 숨겨 최초 조회 시 표시되지 않도록 초기화한다.</summary>
    public override void Initialize()
    {
        Shutdown();
    }

    /// <summary>message를 본문에 저장하고 isLastLine에 따라 안내 방향을 변경한다. 화면의 표시 상태는 유지한다.</summary>
    public void SetText(string message, bool isLastLine = false)
    {
        _text.text = message;
        _indicator.SetLastLine(isLastLine);
    }

    /// <summary>message와 isLastLine으로 본문과 안내 방향을 갱신하고 독백 화면을 표시한다.</summary>
    public void Show(string message, bool isLastLine = false)
    {
        SetText(message, isLastLine);
        SetVisible(true);
    }

    /// <summary>본문을 비우고 독백 화면을 숨겨 세션의 표시 상태를 정리한다.</summary>
    public override void Shutdown()
    {
        _text.text = string.Empty;
        _indicator.SetLastLine(false);
        SetVisible(false);
    }
}
