using UnityEngine;

/// <summary>UIManager가 관리하는 화면의 초기화·표시·정리 계약을 제공한다.</summary>
public abstract class GameUI : MonoBehaviour
{
    /// <summary>화면에서 필요한 매니저와 이벤트를 초기화한다.</summary>
    public abstract void Initialize();

    /// <summary>visible에 따라 화면 객체를 표시하거나 숨긴다.</summary>
    public virtual void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>화면이 구독한 이벤트와 외부 참조를 해제한다.</summary>
    public abstract void Shutdown();
}
