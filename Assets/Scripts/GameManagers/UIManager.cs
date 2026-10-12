using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>씬에 미리 배치된 공통 UI의 타입별 조회와 표시 및 세션 연결을 관리한다.</summary>
public sealed class UIManager : MonoBehaviour
{
    [Header("공통 UI 캔버스")]
    [SerializeField] private Canvas _canvas;

    [Header("UI 입력")]
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private InputSystemUIInputModule _inputModule;

    private readonly Dictionary<Type, GameUI> _views = new Dictionary<Type, GameUI>();

    /// <summary>기존 화면을 정리하고 Canvas에 메인 카메라를 연결한다. 활성 EventSystem이 없으면 프리팹의 입력 컴포넌트를 활성화한다.</summary>
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
        _eventSystem.enabled = EventSystem.current == null;
        _inputModule.enabled = _eventSystem.enabled;
        foreach (GameUI view in _canvas.GetComponentsInChildren<GameUI>(true))
        {
            if (_views.ContainsKey(view.GetType()))
            {
                Debug.LogError($"공통 Canvas에 같은 타입의 UI가 중복 배치되었습니다: {view.GetType().Name}", view);
                continue;
            }
            _views.Add(view.GetType(), view);
            view.Initialize();
        }
    }

    /// <summary>초기화된 씬의 T 화면을 반환한다. 배치되지 않은 타입은 오류를 보고하고 null을 반환한다.</summary>
    public T Get<T>() where T : GameUI
    {
        if (_views.TryGetValue(typeof(T), out GameUI view) && view != null) return (T)view;
        Debug.LogError($"공통 Canvas에 {typeof(T).Name} 화면이 배치되지 않았습니다.", this);
        return null;
    }

    /// <summary>생성된 T 화면을 표시한다.</summary>
    public void Show<T>() where T : GameUI
    {
        T view = Get<T>();
        if (view == null) return;
        view.SetVisible(true);
    }

    /// <summary>이미 생성된 T 화면을 숨긴다. 해당 타입이 캐시에 없으면 새 화면을 생성하지 않는다.</summary>
    public void Hide<T>() where T : GameUI
    {
        if (!_views.TryGetValue(typeof(T), out GameUI view) || view == null) return;
        view.SetVisible(false);
    }

    /// <summary>배치된 화면의 연결과 캐시를 정리하고 공통 Canvas와 자체 UI 입력을 비활성화한다. 화면 객체는 유지한다.</summary>
    public void Shutdown()
    {
        foreach (GameUI view in _views.Values)
        {
            if (view == null) continue;
            view.Shutdown();
        }
        _views.Clear();
        _inputModule.enabled = false;
        _eventSystem.enabled = false;
        _canvas.enabled = false;
    }

    void OnDestroy()
    {
        Shutdown();
    }
}
