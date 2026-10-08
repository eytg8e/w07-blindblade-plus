using UnityEngine;

/// <summary>
/// 충돌과 명시적인 소리 재생 지점에서 공통 감각 사건을 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SensorySource : MonoBehaviour
{
    [Header("충돌")]
    [SerializeField] private bool _reportCollisions = true;
    [SerializeField] private LayerMask _collisionLayers = ~0;
    [SerializeField, Min(0f), Tooltip("표현할 최소 충격량(뉴턴·초)")]
    private float _minimumImpulse = 0.1f;
    [SerializeField, Min(0.01f), Tooltip("강도 1에 해당하는 충격량(뉴턴·초)")]
    private float _fullIntensityImpulse = 5f;
    [SerializeField, Min(0f), Tooltip("같은 발생원의 충돌 재전달 최소 간격(초)")]
    private float _collisionInterval = 0.1f;
    private float _lastCollisionTime = float.NegativeInfinity;
    private SensoryEmitter _emitter;
    private Rigidbody _body;

    [Header("명시적 소리")]
    [SerializeField, Tooltip("선택적 재생 클립. 없으면 ReportSound로 이미 재생한 소리 위치만 알림")]
    private AudioClip _soundClip;
    [SerializeField, Range(0f, 1f)] private float _soundIntensity = 1f;

    void Awake()
    {
        _body = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (_fullIntensityImpulse <= 0f || _minimumImpulse < 0f || _collisionInterval < 0f
            || !Game.IsInitialized || Game.Instance.Emitter == null)
        {
            Debug.LogError("SensorySource의 충격량 설정과 Game의 공통 Emitter 연결을 확인해야 합니다.", this);
            enabled = false;
            return;
        }
        _emitter = Game.Instance.Emitter;
    }

    void OnCollisionEnter(Collision collision)
    {
        float impulse = collision.impulse.magnitude;
        if (!CanReportCollision(collision.gameObject.layer, impulse)
            || Time.time - _lastCollisionTime < _collisionInterval || collision.contactCount == 0)
            return;
        // 양쪽에 활성 발생원이 있으면 Rigidbody 보유 쪽을 우선하고 같은 조건에서는 ID가 작은 쪽만 전달한다.
        if (collision.gameObject.TryGetComponent(out SensorySource other)
            && other.isActiveAndEnabled && other.CanReportCollision(gameObject.layer, impulse)
            && ((_body == null && other._body != null)
                || ((_body != null) == (other._body != null) && GetInstanceID() > other.GetInstanceID())))
            return;
        _lastCollisionTime = Time.time;
        ContactPoint contact = collision.GetContact(0);
        ReportCollision(contact.point, contact.normal, collision.relativeVelocity,
            Mathf.Clamp01(impulse / _fullIntensityImpulse));
    }

    /// <summary>
    /// 대상 레이어와 충격량으로 이 발생원이 충돌을 담당할 수 있는지 반환한다.
    /// </summary>
    private bool CanReportCollision(int layer, float impulse)
    {
        return _reportCollisions && (_collisionLayers.value & (1 << layer)) != 0 && impulse >= _minimumImpulse;
    }

    /// <summary>
    /// 접촉 위치·법선·방향과 정규화 강도의 충돌 사건을 전달하고 시각 생성 여부를 반환한다.
    /// </summary>
    public bool ReportCollision(Vector3 position, Vector3 normal, Vector3 direction, float intensity)
    {
        return Submit(SensoryEvent.CreateCollision(transform, position, normal, direction, intensity));
    }

    /// <summary>
    /// 설정된 클립을 기존 사운드 서비스로 재생하고 현재 위치의 시각 사건을 별도로 알린다.
    /// </summary>
    public void PlaySound()
    {
        if (_soundClip == null)
        {
            Debug.LogError("PlaySound를 호출하려면 SensorySource에 재생 클립을 연결해야 합니다.", this);
            return;
        }
        // 기존 SoundManager는 위치 없는 재생 API이며 공간 음향으로 취급하지 않는다.
        Managers.Sound.Play(_soundClip);
        ReportSound(_soundIntensity);
    }

    /// <summary>
    /// 이미 재생된 소리의 현재 위치와 강도를 공통 담당자에게 전달하고 시각 생성 여부를 반환한다.
    /// </summary>
    public bool ReportSound(float intensity = 1f)
    {
        return Submit(SensoryEvent.CreateSound(transform, transform.position, intensity));
    }

    /// <summary>
    /// 캐싱한 공통 담당자에게 사건을 전달하고 담당자 파괴 시 원인을 보고하여 동작을 중단한다.
    /// </summary>
    private bool Submit(SensoryEvent sensoryEvent)
    {
        if (_emitter == null)
        {
            Debug.LogError("SensorySource의 공통 Emitter가 준비되지 않았거나 파괴되었습니다.", this);
            enabled = false;
            return false;
        }
        return _emitter.Emit(sensoryEvent);
    }
}
