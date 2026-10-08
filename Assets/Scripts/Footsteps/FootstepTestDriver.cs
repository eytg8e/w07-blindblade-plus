using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootstepTestDriver : MonoBehaviour
{
    [Header("테스트 씬 연결")]
    [SerializeField] private Transform _character;
    [SerializeField] private Transform _observer;
    [SerializeField] private FootstepEmitter _emitter;
    [SerializeField, Tooltip("선택적 파동 시연용 독립 프리팹")]
    private FootstepWave _wavePrefab;
    [Header("자동 시연")]
    [SerializeField, Min(0.01f), Tooltip("자동 이동 속도(미터/초)")]
    private float _moveSpeed = 2f;
    [SerializeField, Min(0.01f), Tooltip("중심에서 이동 끝점까지의 거리(미터)")]
    private float _travelRadius = 6f;
    [SerializeField, Min(0.01f), Tooltip("관측자가 멀어졌다 돌아오는 주기(초)")]
    private float _observerPeriod = 12f;
    private float _elapsed;
    private int _eventCount;
    private bool _playWaves = true;
    private bool _move = true;
    private bool _varyDistance = true;

    void Start()
    {
        if (_wavePrefab != null)
            Managers.Pool.CreatePool(_wavePrefab.gameObject);
    }

    void OnEnable()
    {
        if (_emitter == null)
        {
            Debug.LogError("FootstepTestDriver에 해당 캐릭터의 FootstepEmitter를 연결해야 합니다.", this);
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
        if (_varyDistance)
            _observer.position = new Vector3(Mathf.PingPong(_elapsed / _observerPeriod * 40f, 20f), 0f, 0f);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(15f, 15f, 360f, 280f), GUI.skin.box);
        GUILayout.Label("Footstep system test scene");
        _move = GUILayout.Toggle(_move, "Automatic character movement");
        _emitter.enabled = GUILayout.Toggle(_emitter.enabled, "Emit footsteps");
        _playWaves = GUILayout.Toggle(_playWaves, "Play independent waves on contact");
        if (_wavePrefab != null && GUILayout.Button("Play wave without a footstep"))
            PlayWave(new Vector3(_character.position.x, 0.01f, _character.position.z));
        _varyDistance = GUILayout.Toggle(_varyDistance, "Near / far observer sweep (0 - 20 m)");
        GUILayout.Label("Footstep events: " + _eventCount);
        GUILayout.Label("Blue: left / Orange: right / Cyan: wave");
        if (GUILayout.Button("Teleport character (+10 m)"))
        {
            _move = false;
            _character.position += Vector3.forward * 10f;
        }
        if (GUILayout.Button("Return observer to center"))
        {
            _varyDistance = false;
            _observer.position = Vector3.zero;
        }
        GUILayout.EndArea();
    }

    /// <summary>
    /// 해당 캐릭터의 지면 접촉 위치를 받아 발생 횟수를 증가시키고 선택한 경우 독립 파동을 재생한다.
    /// </summary>
    private void HandleFootstep(Transform character, Vector3 position)
    {
        if (character != _character)
            return;
        _eventCount++;
        if (_playWaves && _wavePrefab != null)
            PlayWave(position + Vector3.up * 0.01f);
    }
    /// <summary>
    /// 지정한 월드 위치에 독립 파동 프리팹을 생성하여 자체 수명으로 재생한다.
    /// </summary>
    private void PlayWave(Vector3 position)
    {
        GameObject instance = Managers.Resource.Instantiate(_wavePrefab.gameObject,
            position, Quaternion.identity);
        if (instance != null)
            instance.GetComponent<FootstepWave>().Play();
    }
}
