using UnityEngine;

/// <summary>Simple target used only by PlayerTestWorldAdapter.</summary>
public sealed class PlayerTestTarget : MonoBehaviour
{
    [SerializeField] private string _targetId;
    [SerializeField] private Collider _hitCollider;

    public string TargetId => string.IsNullOrWhiteSpace(_targetId) ? gameObject.name : _targetId;
    public bool IsAlive { get; private set; } = true;
    private Vector3 _worldPositionOffset;

    public Vector3 WorldPosition
    {
        get
        {
            if (_hitCollider == null)
                _hitCollider = GetComponentInChildren<Collider>();
            return _hitCollider != null ? _hitCollider.bounds.center : transform.position;
        }
    }

private void Awake()
    {
        if (_hitCollider == null)
            _hitCollider = GetComponentInChildren<Collider>();

        if (_hitCollider != null)
            _worldPositionOffset = _hitCollider.bounds.center - transform.position;
    }

    /// <summary>테스트 대상의 식별자, 충돌체, 반경, 생존 상태로 전투 정보를 반환합니다.</summary>
    public PlayerCombatTarget CreateCombatTarget()
    {
        if (_hitCollider == null)
            _hitCollider = GetComponentInChildren<Collider>();

        float radius = _hitCollider != null
            ? Mathf.Max(_hitCollider.bounds.extents.x, _hitCollider.bounds.extents.z)
            : 0.35f;

        return new PlayerCombatTarget(TargetId, WorldPosition, _hitCollider, radius, IsAlive);
    }

/// <summary>테스트 시나리오를 위해 대상을 지정된 중심 위치에 되살리고 충돌 판정을 복구합니다.</summary>
    public void ResetForTest(Vector3 worldCenter)
    {
        if (_hitCollider == null)
            _hitCollider = GetComponentInChildren<Collider>();
        if (gameObject.activeInHierarchy && _hitCollider != null)
            _worldPositionOffset = _hitCollider.bounds.center - transform.position;

        gameObject.SetActive(true);
        transform.position = worldCenter - _worldPositionOffset;
        IsAlive = true;

        if (_hitCollider != null)
            _hitCollider.enabled = true;
    }


    /// <summary>테스트 대상의 사망을 확정하고 충돌체와 게임 오브젝트를 비활성화합니다.</summary>
    public void ConfirmIaidoDeath()
    {
        if (!IsAlive)
            return;

        IsAlive = false;
        if (_hitCollider != null)
            _hitCollider.enabled = false;
        gameObject.SetActive(false);
    }
}
