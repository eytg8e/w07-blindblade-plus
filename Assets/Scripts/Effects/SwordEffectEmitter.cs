using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 베기 구간에 검 궤적과 접촉을 관찰해 Trail 및 공용 Hit를 자동 요청한다.
/// 피해·가드·사망 판정은 수행하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SwordEffectEmitter : MonoBehaviour
{
    private const int CONTACT_CAPACITY = 32;
    private const int SWEEP_SAMPLES = 5;

    [Header("검 연결")]
    [SerializeField] private Transform _bladeBase;
    [SerializeField] private Transform _bladeTip;
    [SerializeField] private Transform _ownerRoot;

    [Header("시각적 접촉 감지")]
    [SerializeField] private LayerMask _contactLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.001f)] private float _contactRadius = 0.04f;
    [SerializeField, Min(0.1f)] private float _teleportDistance = 3f;
    private readonly RaycastHit[] _castHits = new RaycastHit[CONTACT_CAPACITY];
    private readonly Collider[] _overlaps = new Collider[CONTACT_CAPACITY];
    private readonly HashSet<Collider> _reportedContacts = new HashSet<Collider>();
    private EffectManager _effects;
    private Vector3 _previousBase;
    private Vector3 _previousTip;
    private int _trailHandle;
    private bool _swinging;
    private bool _reportedCapacity;
    public bool IsSwinging => _swinging;

    void Start()
    {
        _effects = Game.Instance.EffectManager;
    }

    void LateUpdate()
    {
        if (!_swinging || Time.deltaTime <= 0f)
            return;
        if (_effects == null || !_effects.IsReady || !HasActiveEndpoints())
        {
            CancelSwing();
            return;
        }
        DetectContacts();
    }

    /// <summary>
    /// 검의 이전·현재 끝점과 감지 설정으로 이동 경로 및 현재 접촉을 확인한다.
    /// 고정된 공용 Hit를 요청하고 다음 감지에 사용할 끝점 위치를 저장한다.
    /// </summary>
    private void DetectContacts()
    {
        Vector3 bladeBase = _bladeBase.position;
        Vector3 bladeTip = _bladeTip.position;
        float movement = Mathf.Max(Vector3.Distance(_previousBase, bladeBase),
            Vector3.Distance(_previousTip, bladeTip));
        if (movement < _teleportDistance)
        {
            for (int i = 0; i < SWEEP_SAMPLES; i++)
            {
                float progress = i / (float)(SWEEP_SAMPLES - 1);
                SweepContact(Vector3.Lerp(_previousBase, _previousTip, progress),
                    Vector3.Lerp(bladeBase, bladeTip, progress));
            }
        }
        SweepContact(bladeBase, bladeTip);
        DetectOverlaps(bladeBase, bladeTip);
        _previousBase = bladeBase;
        _previousTip = bladeTip;
    }

    void OnDisable()
    {
        CancelSwing();
    }

    /// <summary>
    /// Inspector의 검 끝점·소유자·레이어 설정으로 베기 표현과 접촉 감지를 시작한다.
    /// 이전 요청을 취소하고 Trail 핸들을 보관하며 시작 성공 여부를 반환한다.
    /// </summary>
    public bool BeginSwing()
    {
        if (!isActiveAndEnabled || _effects == null || !_effects.IsReady
            || !HasActiveEndpoints() || _ownerRoot == null)
        {
            Debug.LogWarning("[SwordEffectEmitter] BeginSwing requires Start initialization, active blade endpoints and an owner root.", this);
            return false;
        }
        CancelSwing();
        _trailHandle = _effects.BeginTrail(_bladeBase, _bladeTip);
        if (_trailHandle == 0)
            return false;
        _previousBase = _bladeBase.position;
        _previousTip = _bladeTip.position;
        _reportedCapacity = false;
        _swinging = true;
        return true;
    }

    /// <summary>
    /// 입력 없이 현재 검 접촉 감지와 Trail 기록을 종료한다.
    /// Trail 잔상은 자연 종료하도록 유지하고 보관 핸들과 중복 기록을 해제한다.
    /// </summary>
    public void EndSwing()
    {
        if (_swinging && Time.deltaTime > 0f && _effects != null
            && _effects.IsReady && HasActiveEndpoints())
            DetectContacts();
        if (_effects != null)
            _effects.EndTrail(_trailHandle);
        _trailHandle = 0;
        _swinging = false;
        _reportedContacts.Clear();
    }

    /// <summary>
    /// 입력 없이 진행 중인 베기 감지와 Trail을 즉시 정리한다.
    /// 취소·무기 교체·비활성화 시 핸들과 중복 기록을 해제한다.
    /// </summary>
    public void CancelSwing()
    {
        if (_effects != null)
            _effects.StopTrail(_trailHandle);
        _trailHandle = 0;
        _swinging = false;
        _reportedContacts.Clear();
    }

    /// <summary>
    /// 설정한 검 양 끝점의 참조와 계층 활성 상태를 확인한다.
    /// 두 끝점이 모두 존재하고 활성 상태일 때 true를 반환한다.
    /// </summary>
    private bool HasActiveEndpoints()
    {
        return _bladeBase != null && _bladeTip != null
            && _bladeBase.gameObject.activeInHierarchy && _bladeTip.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// from에서 to까지 구를 이동시켜 감지 레이어의 가장 가까운 외부 접촉점을 찾는다.
    /// 자기 소유 충돌체를 제외하고 실제 RaycastHit.point에 공용 Hit를 요청한다.
    /// </summary>
    private void SweepContact(Vector3 from, Vector3 to)
    {
        Vector3 movement = to - from;
        float distance = movement.magnitude;
        if (distance <= 0.0001f)
            return;
        int count = Physics.SphereCastNonAlloc(from, _contactRadius, movement / distance,
            _castHits, distance, _contactLayers, QueryTriggerInteraction.Collide);
        ReportCapacity(count);
        float nearestDistance = float.PositiveInfinity;
        int nearest = -1;
        for (int i = 0; i < count; i++)
        {
            if (!IsExternalCollider(_castHits[i].collider) || _castHits[i].distance <= 0f
                || _castHits[i].distance >= nearestDistance)
                continue;
            nearestDistance = _castHits[i].distance;
            nearest = i;
        }
        if (nearest >= 0)
            PlayContact(_castHits[nearest].collider, _castHits[nearest].point);
    }

    /// <summary>
    /// 현재 검 구간과 겹치는 감지 레이어 충돌체를 찾아 시작 중첩과 정지 접촉을 보완한다.
    /// 검의 이전 양 끝점·현재 양 끝점 중 가장 가까운 표면 후보 위치에 Hit를 요청한다.
    /// </summary>
    private void DetectOverlaps(Vector3 bladeBase, Vector3 bladeTip)
    {
        int count = Physics.OverlapCapsuleNonAlloc(bladeBase, bladeTip, _contactRadius,
            _overlaps, _contactLayers, QueryTriggerInteraction.Collide);
        ReportCapacity(count);
        for (int i = 0; i < count; i++)
        {
            Collider collider = _overlaps[i];
            if (!IsExternalCollider(collider) || _reportedContacts.Contains(collider))
                continue;
            Vector3 reference = bladeBase;
            float nearestDistance = float.PositiveInfinity;
            SelectSurfaceReference(collider, _previousBase, ref reference, ref nearestDistance);
            SelectSurfaceReference(collider, _previousTip, ref reference, ref nearestDistance);
            SelectSurfaceReference(collider, bladeBase, ref reference, ref nearestDistance);
            SelectSurfaceReference(collider, bladeTip, ref reference, ref nearestDistance);
            PlayContact(collider, collider.ClosestPoint(reference));
        }
    }

    /// <summary>
    /// collider 바깥의 후보 위치에서 가장 가까운 표면 거리를 비교한다.
    /// candidate를 사용해 reference와 nearestDistance를 갱신하고 내부 후보는 제외한다.
    /// </summary>
    private void SelectSurfaceReference(Collider collider, Vector3 candidate,
        ref Vector3 reference, ref float nearestDistance)
    {
        float distance = (candidate - collider.ClosestPoint(candidate)).sqrMagnitude;
        if (distance > 0.000001f && distance < nearestDistance)
        {
            reference = candidate;
            nearestDistance = distance;
        }
    }

    /// <summary>
    /// collider가 검 컴포넌트와 소유자 계층 바깥에 있는지 확인한다.
    /// 유효한 외부 충돌체이면 true를 반환한다.
    /// </summary>
    private bool IsExternalCollider(Collider collider)
    {
        return collider != null && !collider.transform.IsChildOf(transform)
            && (_ownerRoot == null || !collider.transform.IsChildOf(_ownerRoot));
    }

    /// <summary>
    /// collider의 접촉 position에 Library에 등록된 공용 타격 효과를 요청한다.
    /// 성공한 충돌체를 기록해 같은 베기에서 한 번만 재생한다.
    /// </summary>
    private void PlayContact(Collider collider, Vector3 position)
    {
        if (!_reportedContacts.Contains(collider) && _effects.PlayHit(position))
            _reportedContacts.Add(collider);
    }

    /// <summary>
    /// count가 접촉 버퍼 용량에 도달했는지 확인한다.
    /// 결과 누락 가능성을 베기당 한 번 알리고 안내 상태를 기록한다.
    /// </summary>
    private void ReportCapacity(int count)
    {
        if (count < CONTACT_CAPACITY || _reportedCapacity)
            return;
        _reportedCapacity = true;
        Debug.LogWarning("[SwordEffectEmitter] Contact buffer is full. Narrow the contact layers; some contacts may be omitted.", this);
    }
}
