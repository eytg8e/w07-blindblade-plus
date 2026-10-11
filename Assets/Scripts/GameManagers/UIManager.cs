using System;
using System.Collections.Generic;

using UnityEngine;

/// <summary>공통 UI를 생성하고 화면 타입별 조회와 표시 및 씬 수명을 관리한다.</summary>
public sealed class UIManager : MonoBehaviour
{
    [Header("공통 UI 캔버스")]
    [SerializeField] private Canvas _canvas;

    private readonly Dictionary<Type, GameUI> _views = new Dictionary<Type, GameUI>();

    /// <summary>기존 화면을 정리하고 필요한 메인 카메라 연결과 UI 입력용 이벤트 시스템을 준비한다.</summary>
    public void Initialize()
    {
        Shutdown();
        _canvas.worldCamera = Camera.main;
        if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay && _canvas.worldCamera == null)
        {
            Debug.LogError("공통 UI 캔버스에 연결할 MainCamera가 없습니다.", this);
            _canvas.enabled = false;
            return;
        }
        _canvas.enabled = true;
    }

    /// <summary>T 화면을 최초 조회 시 리소스에서 생성·초기화하고 이후에는 캐싱한 화면을 반환한다.</summary>
    public T Get<T>() where T : GameUI
    {
        if (_views.TryGetValue(typeof(T), out GameUI view) && view != null) return (T)view;
        var path = ResourcePaths.UI_PATH + typeof(T).Name;
        GameObject instance = Managers.Resource.Instantiate(path, _canvas.transform, usePool: false);
        if (instance == null) return null;
        T screen = instance.GetComponent<T>();
        if (screen == null)
        {
            Debug.LogError($"UI 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다: {path}", instance);
            Managers.Resource.Destroy(instance);
            return null;
        }
        _views[typeof(T)] = screen;
        screen.Initialize();
        return screen;
    }

    /// <summary>생성된 T 화면을 표시한다.</summary>
    public void Show<T>() where T : GameUI
    {
        T view = Get<T>();
        if (view == null) return;
        view.SetVisible(true);
    }

    /// <summary>생성된 T 화면을 숨긴다.</summary>
    public void Hide<T>() where T : GameUI
    {
        T view = Get<T>();
        if (view == null) return;
        view.SetVisible(false);
    }

    /// <summary>모든 화면의 연결을 해제하고 생성한 화면을 제거한다.</summary>
    public void Shutdown()
    {
        foreach (GameUI view in _views.Values)
        {
            if (view == null) continue;
            view.Shutdown();
            Managers.Resource.Destroy(view.gameObject);
        }
        _views.Clear();
    }

    void OnDestroy()
    {
        Shutdown();
    }
}
