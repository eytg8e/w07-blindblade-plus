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
    /// <summary>
    /// 싱글턴 인스턴스를 반환합니다. 인스턴스가 없으면 자동으로 생성합니다.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
#if UNITY_6000_0_OR_NEWER
                _instance = FindAnyObjectByType<T>();      
#else
                    _instance = FindObjectOfType<T>();
#endif
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name);
                    _instance = singletonObject.AddComponent<T>();
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
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}