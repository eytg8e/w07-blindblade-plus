using System;

using UnityEngine;

/// <summary>
/// MonoBehaviour 기반 싱글턴 패턴을 구현한 클래스입니다.
/// DontDestroyOnLoad를 사용하여 씬 전환 시에도 유지됩니다.
/// 인스턴스가 없을 경우 자동으로 생성합니다.
/// </summary>
/// <typeparam name="T">싱글턴으로 만들 컴포넌트 타입</typeparam>
public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _quitting;

    protected static T CurrentInstance => _instance;
    protected virtual bool IncludeInactiveInstances => false;
    protected virtual string SingletonObjectName => typeof(T).Name;
    public static bool IsQuitting => _quitting;
    /// <summary>
    /// 싱글턴 인스턴스를 반환합니다. 인스턴스가 없으면 자동으로 생성합니다.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                if (_quitting || !Application.isPlaying)
                    throw new InvalidOperationException($"{typeof(T).Name} can only be created during Play Mode.");

                T candidate = FindSingleton();
                if (candidate != null)
                    (candidate as MonoSingleton<T>).InitializeSingleton();

                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name);
                    _instance = singletonObject.AddComponent<T>();
                    singletonObject.name = (_instance as MonoSingleton<T>).SingletonObjectName;
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 싱글턴 인스턴스가 초기화되었는지 여부를 반환합니다.
    /// </summary>
    public static bool IsInitialized => _instance != null;
        
    protected virtual void Awake()
    {
        InitializeSingleton();
    }

    /// <summary>
    /// 씬에서 기존 싱글턴 컴포넌트를 찾아 반환한다.
    /// </summary>
    protected static T FindSingleton()
    {
        foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate.gameObject.scene.IsValid()
                && (candidate.gameObject.activeInHierarchy
                    || (candidate as MonoSingleton<T>).IncludeInactiveInstances))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// 현재 컴포넌트를 싱글턴으로 등록하고 중복 인스턴스를 제거한다.
    /// </summary>
    protected virtual void InitializeSingleton()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            DestroyDuplicate();
        }
    }

    /// <summary>
    /// 중복 싱글턴의 게임 오브젝트를 제거한다.
    /// </summary>
    protected virtual void DestroyDuplicate()
    {
        Destroy(gameObject);
    }

    protected virtual void OnApplicationQuit()
    {
        if (_instance == this)
            _quitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
