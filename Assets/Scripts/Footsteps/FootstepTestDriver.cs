using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootstepTestDriver : MonoBehaviour
{
    [Header("시연 씬 연결")]
    [SerializeField] private Transform _character;
    [SerializeField] private Transform _observer;
    [SerializeField] private Transform _alternateObserver;
    [SerializeField] private FootstepEmitter _emitter;

    [Header("자동 시연")]
    [SerializeField, Min(0.01f), Tooltip("자동 이동 속도(미터/초)")]
    private float _moveSpeed = 2f;
    [SerializeField, Min(0.01f), Tooltip("중심에서 이동 끝점까지의 거리(미터)")]
    private float _travelRadius = 6f;
    [SerializeField, Min(0.01f), Tooltip("관측자가 멀어졌다 돌아오는 주기(초)")]
    private float _observerPeriod = 12f;
    private SensoryManager _sensoryManager;
    private float _elapsed;
    private int _eventCount;
    private bool _move = true;
    private bool _varyDistance = true;

    void Start()
    {
        _sensoryManager = Game.IsInitialized ? Game.Instance.SensoryManager : null;
        if (_character == null || _observer == null || _alternateObserver == null
            || _sensoryManager == null)
        {
            Debug.LogError("감각 시연 씬의 캐릭터·관측자·공통 Emitter 참조를 연결해야 합니다.", this);
            enabled = false;
            return;
        }
        _sensoryManager.SetObserver(_observer);
        if (!_sensoryManager.IsReady && !_sensoryManager.Initialize())
        {
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        if (_emitter == null)
        {
            Debug.LogError("FootstepTestDriver에 캐릭터의 FootstepEmitter를 연결해야 합니다.", this);
            enabled = false;
            return;
        }
        _emitter.OnFootstepEmitted += HandleFootstep;
    }

    void OnDisable()
    {
        if (_emitter != null)
            _emitter.OnFootstepEmitted -= HandleFootstep;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_move)
        {
            float z = Mathf.PingPong(_elapsed * _moveSpeed, _travelRadius * 2f) - _travelRadius;
            _character.position = new Vector3(0f, 0.05f, z);
        }
        if (_varyDistance && _sensoryManager.Observer != null)
            _sensoryManager.Observer.position = new Vector3(
                Mathf.PingPong(_elapsed / _observerPeriod * 40f, 20f), 0f, 0f);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(15f, 15f, 380f, 520f), GUI.skin.box);
        GUILayout.Label("감각 표현: 걸음");
        _move = GUILayout.Toggle(_move, "캐릭터 자동 이동");
        _emitter.enabled = GUILayout.Toggle(_emitter.enabled, "걸음 감지");
        _varyDistance = GUILayout.Toggle(_varyDistance, "관측자 거리 왕복 (0~20m)");
        GUILayout.Label("걸음 접촉: " + _eventCount + " / 활성 효과: " + _sensoryManager.ActiveEffectCount);
        GUILayout.Label("거리 기준: 완전 표시 5m / 최대 15m");
        if (GUILayout.Button("순간이동 (+10m)"))
        {
            _move = false;
            _character.position += Vector3.forward * 10f;
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("가까움"))
            MoveObserver(0f);
        if (GUILayout.Button("감쇠 10m"))
            MoveObserver(10f);
        if (GUILayout.Button("경계 15m"))
            MoveObserver(15f);
        if (GUILayout.Button("범위 밖"))
            MoveObserver(20f);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("관측자 교체"))
        {
            Transform next = _sensoryManager.Observer == _observer ? _alternateObserver : _observer;
            _sensoryManager.SetObserver(next);
        }
        if (GUILayout.Button("관측자 해제 (남은 효과 숨김)"))
        {
            _emitter.enabled = false;
            _sensoryManager.ClearObserver();
        }
        if (GUILayout.Button("세션 종료 (효과 반환)"))
        {
            _emitter.enabled = false;
            _sensoryManager.EndSession();
        }
        if (GUILayout.Button("관측자 복구 / 세션 재시작"))
        {
            _sensoryManager.SetObserver(_observer);
            if (_sensoryManager.Initialize())
                _emitter.enabled = true;
        }
        GUILayout.EndArea();
    }

    /// <summary>
    /// 캐릭터에서 지정한 거리로 현재 관측자를 이동하여 거리 경계를 시연한다.
    /// </summary>
    private void MoveObserver(float distance)
    {
        _varyDistance = false;
        if (_sensoryManager.Observer != null)
            _sensoryManager.Observer.position = _character.position + Vector3.right * distance;
    }

    /// <summary>
    /// 캐릭터의 지면 접촉 이벤트를 받아 표시 횟수만 갱신하고 시각 효과는 생성하지 않는다.
    /// </summary>
    private void HandleFootstep(Transform character, Vector3 position)
    {
        if (character == _character)
            _eventCount++;
    }
}
