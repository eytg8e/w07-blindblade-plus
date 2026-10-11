using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackSelector : MonoBehaviour
{
    [Header("공격 패턴 목록")]
    [SerializeField, Tooltip("등록한 같은 루트의 공격만 사용합니다. 비어 있으면 공격하지 않습니다.")]
    private EnemyAttackBehaviour[] _patterns = new EnemyAttackBehaviour[0];
    [Header("근접 프로토타입")]
    [SerializeField] private bool _meleeOnly;
    private EnemyAttackBehaviour _currentAttack;
    private int _lastSelectedIndex = -1;
    private float _staggerUntil;

    public EnemyAttackBehaviour CurrentAttack => _currentAttack;
    public bool IsBusy => _currentAttack != null && _currentAttack.IsBusy;
    public bool IsStaggered => Time.time < _staggerUntil;
    public float StaggerRemaining => Mathf.Max(0f, _staggerUntil - Time.time);
    public bool MeleeOnly => _meleeOnly;

    void OnDisable()
    {
        CancelCurrentAttack();
        _lastSelectedIndex = -1;
        _staggerUntil = 0f;
    }

    /// <summary>
    /// target이 활성 패턴 중 하나의 최소·최대 사거리에 들어가면 true를 반환한다.
    /// 쿨타임은 제외하여 사용 대기 중에도 적이 공격 위치를 유지할 수 있게 한다.
    /// </summary>
    public bool CanAttack(Transform target)
    {
        foreach (EnemyAttackBehaviour pattern in _patterns)
        {
            if (pattern != null && (!_meleeOnly || pattern is EnemyMeleeAttack) && pattern.gameObject == gameObject && pattern.CanAttack(target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// target의 사거리와 패턴 쿨타임, 우선순위를 사용하여 한 패턴의 전조를 시작한다.
    /// 같은 우선순위는 마지막 선택 다음 순서로 순환하며 성공 시 현재 패턴을 저장하고 true를 반환한다.
    /// </summary>
    public bool TryAttack(Transform target)
    {
        if (!isActiveAndEnabled || IsBusy || Time.time < _staggerUntil)
            return false;

        int selectedIndex = -1;
        int highestPriority = int.MinValue;
        for (int offset = 1; offset <= _patterns.Length; offset++)
        {
            int index = (_lastSelectedIndex + offset) % _patterns.Length;
            EnemyAttackBehaviour pattern = _patterns[index];
            if (pattern == null || (_meleeOnly && !(pattern is EnemyMeleeAttack)) || pattern.gameObject != gameObject || !pattern.IsAvailable ||
                !pattern.CanAttack(target) || (selectedIndex >= 0 && pattern.Priority <= highestPriority))
                continue;

            selectedIndex = index;
            highestPriority = pattern.Priority;
        }

        if (selectedIndex < 0)
            return false;

        _currentAttack = _patterns[selectedIndex];
        _lastSelectedIndex = selectedIndex;
        return _currentAttack.TryAttack(target);
    }

    /// <summary>
    /// target까지의 거리를 사용하여 아직 도달하지 못한 활성 패턴의 최대 사거리를 반환한다.
    /// 최소 사거리 사이의 빈 구간에서는 더 가까운 공격으로 접근하며 패턴이 없으면 null을 반환한다.
    /// </summary>
    public float? GetApproachRange(Transform target)
    {
        if (target == null)
            return null;

        float distance = Vector3.Distance(transform.position, target.position);
        float? range = null;
        foreach (EnemyAttackBehaviour pattern in _patterns)
        {
            if (pattern == null || (_meleeOnly && !(pattern is EnemyMeleeAttack)) || pattern.gameObject != gameObject || !pattern.isActiveAndEnabled || distance <= pattern.Range)
                continue;

            if (!range.HasValue || pattern.Range > range.Value)
                range = pattern.Range;
        }

        return range;
    }

    /// <summary>
    /// 현재 패턴에 취소를 전달하고 현재 공격 참조를 제거한다.
    /// 입력값 없이 전조·지속 공격·후딜을 정리하여 대상 변경이나 풀 반환에 사용한다.
    /// </summary>
    public void CancelCurrentAttack()
    {
        EnemyAttackBehaviour attack = _currentAttack;
        _currentAttack = null;
        if (attack != null)
            attack.CancelAttack();
    }

    /// <summary>duration 동안 새 공격을 제한하고 현재 공격을 취소한다. 피격 후 연속 공격을 잠시 중단한다.</summary>
    public void Stagger(float duration)
    {
        float remaining = _currentAttack != null ? _currentAttack.RecoveryRemaining : 0f;
        CancelCurrentAttack();
        _staggerUntil = Mathf.Max(_staggerUntil, Time.time + Mathf.Max(duration, remaining));
    }
}
