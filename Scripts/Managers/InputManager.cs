using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class PlayerInputController : MonoBehaviour
{
    public static PlayerInputController Instance { get; private set; }

    private InputSystem_Actions actions;

    
    public event Action<InputAction.CallbackContext> Move;
    public event Action<InputAction.CallbackContext> OpenSetting;

    // 구독 해제용으로 등록한 액션들을 저장
    private readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> subscriptions = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        if (actions == null)
            return;

        actions.Player.Enable();

        Bind(actions.Player.Move, ctx => Move?.Invoke(ctx));
        Bind(actions.UI.Cancel, ctx => OpenSetting?.Invoke(ctx));
    }

    private void OnDisable()
    {
        if (actions == null)
            return;

        foreach (var (action, handler) in subscriptions)
        {
            Unbind(action, handler);
        }

        subscriptions.Clear();

        actions.Player.Disable();
    }

    // started/performed/canceled 세 이벤트를 한 번에 구독하고 해제 목록에 등록
    private void Bind(InputAction action, Action<InputAction.CallbackContext> handler)
    {
        action.started += handler;
        action.performed += handler;
        action.canceled += handler;

        subscriptions.Add((action, handler));
    }
    private void Unbind(InputAction action, Action<InputAction.CallbackContext> handler)
    {
        action.started -= handler;
        action.performed -= handler;
        action.canceled -= handler;
    }
}