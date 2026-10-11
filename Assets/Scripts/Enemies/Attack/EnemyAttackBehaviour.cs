using System;

using UnityEngine;

public enum EnemyAttackPhase
{
    Ready,
    Telegraph,
    Active,
    Recovery
}

[RequireComponent(typeof(EnemySensor))]
public abstract class EnemyAttackBehaviour : MonoBehaviour
{
    [Header("패턴 선택 설정")]
    [SerializeField] private string _patternId;
    [SerializeField] private int _priority;
    [SerializeField, Min(0f)] private float _minimumRange;

    [Header("전조 및 공격 설정")]
    [SerializeField, Min(0.1f)] private float _telegraphDuration = 1f;
    [SerializeField, Min(0f)] private float _recoveryDuration = 0.75f;
    [SerializeField, Min(0.02f)] private float _cooldown = 2f;
    [SerializeField] private bool _cancelWhenHidden = true;
    private EnemySensor _sensor;
    private EnemyAttackPhase _phase;
    private EnemyAttackContext _context;
    private float _phaseEndTime;
    private float _nextAttackTime;

    public abstract float Range { get; }
    public string PatternId => string.IsNullOrEmpty(_patternId) ? GetType().Name : _patternId;
    public int Priority => _priority;
    public EnemyAttackPhase Phase => _phase;
    public bool IsBusy => _phase != EnemyAttackPhase.Ready;
    public bool IsAvailable => isActiveAndEnabled && !IsBusy && Time.time >= _nextAttackTime;
    public virtual bool AllowsMovement => false;
    public EnemyAttackContext Context => _context;
    public float ExecutionDuration => ActiveDuration;
    public float RecoveryRemaining => _phase == EnemyAttackPhase.Recovery ? Mathf.Max(0f, _phaseEndTime - Time.time) : 0f;
    public float TelegraphProgress => _phase == EnemyAttackPhase.Telegraph
        ? Mathf.Clamp01(1f - (_phaseEndTime - Time.time) / Mathf.Max(0.1f, _context.TelegraphDuration)) : 1f;
    public event Action<Transform> AttackPerformed;
    public event Action<EnemyAttackContext> TelegraphStarted;
    public event Action<EnemyAttackContext> AttackCommitted;
    public event Action<EnemyAttackContext> AttackEnded;
    public event Action<EnemyAttackContext> AttackCompleted;
    public event Action<EnemyAttackContext> AttackCancelled;

    protected virtual float ActiveDuration => 0f;
    protected virtual bool HasFinishedActiveAttack => Time.time >= _phaseEndTime;
    protected virtual EnemySoundAction SoundAction => EnemySoundAction.None;

    protected virtual void Awake()
    {
        _sensor = GetComponent<EnemySensor>();
    }

    protected virtual void OnEnable()
    {
        _phase = EnemyAttackPhase.Ready;
        _nextAttackTime = 0f;
    }

    protected virtual void Update()
    {
        if (_phase == EnemyAttackPhase.Telegraph)
        {
            if (_context.Target == null || !_context.Target.gameObject.activeInHierarchy ||
                _sensor.Target != _context.Target || (_cancelWhenHidden && !_sensor.CanSeeTarget()))
            {
                CancelAttack();
                return;
            }

            if (Time.time >= _phaseEndTime)
                CommitAttack();
        }
        else if (_phase == EnemyAttackPhase.Active)
        {
            UpdateActiveAttack(_context);
            if (_phase == EnemyAttackPhase.Active && HasFinishedActiveAttack)
                BeginRecovery();
        }
        else if (_phase == EnemyAttackPhase.Recovery && Time.time >= _phaseEndTime)
        {
            EnemyAttackContext context = _context;
            _phase = EnemyAttackPhase.Ready;
            AttackCompleted?.Invoke(context);
        }
    }

    protected virtual void OnDisable()
    {
        CancelAttack();
    }

    /// <summary>
    /// target의 활성 상태와 최소·최대 사거리를 검사하여 패턴 사용 범위 안이면 true를 반환한다.
    /// 쿨타임과 시야는 검사하지 않으며 선택기가 공격 가능한 위치를 판단할 때 사용한다.
    /// </summary>
    public virtual bool CanAttack(Transform target)
    {
        if (!isActiveAndEnabled || target == null || !target.gameObject.activeInHierarchy)
            return false;

        float distanceSquared = (target.position - transform.position).sqrMagnitude;
        return distanceSquared >= _minimumRange * _minimumRange && distanceSquared <= Range * Range;
    }

    /// <summary>
    /// target의 사거리, 쿨타임 및 시야가 유효하면 조준 위치를 고정하고 전조를 시작한다.
    /// 다음 사용 시간과 전조 종료 시간을 예약하며 시작에 성공하면 true를 반환한다.
    /// </summary>
    internal bool TryAttack(Transform target)
    {
        if (!IsAvailable || !CanAttack(target) || target != _sensor.Target || !_sensor.CanSeeTarget())
            return false;

        float telegraphDuration = Mathf.Max(0.1f, _telegraphDuration);
        _context = new EnemyAttackContext(this, target, telegraphDuration);
        _phase = EnemyAttackPhase.Telegraph;
        _phaseEndTime = Time.time + telegraphDuration;
        _nextAttackTime = Time.time + _cooldown;
        EnemyAttackContext context = _context;
        PrepareTelegraph(context);
        PlayActionSound(EnemySoundStage.Telegraph);
        TelegraphStarted?.Invoke(context);
        return true;
    }

    /// <summary>
    /// 진행 중인 전조·공격·후딜을 중단하고 현재 공격 문맥을 취소 이벤트에 전달한다.
    /// 파생 공격의 이동 상태를 복원하고 Ready로 전환하며 이미 실행된 피해는 되돌리지 않는다.
    /// </summary>
    public void CancelAttack()
    {
        if (!IsBusy)
            return;

        EnemyAttackContext context = _context;
        _phase = EnemyAttackPhase.Ready;
        OnAttackEnded(context, true);
        AttackCancelled?.Invoke(context);
    }

    /// <summary>
    /// 전조 종료 시 저장된 공격 문맥으로 파생 공격을 실행하고 확정 이벤트를 전달한다.
    /// 조준 위치를 다시 갱신하지 않으며 지속 공격은 Active, 즉시 공격은 후딜로 전환한다.
    /// </summary>
    private void CommitAttack()
    {
        _phase = EnemyAttackPhase.Active;
        _phaseEndTime = Time.time + ActiveDuration;
        EnemyAttackContext context = _context;
        PlayActionSound(EnemySoundStage.Action);
        PerformAttack(context);
        if (_phase != EnemyAttackPhase.Active)
            return;

        AttackCommitted?.Invoke(context);
        if (_phase != EnemyAttackPhase.Active)
            return;
        AttackPerformed?.Invoke(context.Target);
        if (_phase == EnemyAttackPhase.Active && ActiveDuration <= 0f)
            BeginRecovery();
    }

    /// <summary>
    /// 현재 공격 문맥의 실행 종료를 알리고 설정 후딜과 minimumDuration 중 큰 값으로 종료 시간을 예약한다.
    /// 공격 상태를 Recovery로 변경하여 다른 패턴과 동시에 실행되지 않게 한다.
    /// </summary>
    protected void BeginRecovery(float minimumDuration = 0f)
    {
        _phase = EnemyAttackPhase.Recovery;
        _phaseEndTime = Time.time + Mathf.Max(_recoveryDuration, minimumDuration);
        EnemyAttackContext context = _context;
        OnAttackEnded(context, false);
        AttackEnded?.Invoke(context);
    }

    /// <summary>
    /// 행동 종류와 stage로 공통 설정의 소리를 찾아 기존 매니저에 적 위치의 단발 효과음을 요청한다.
    /// 전조 또는 공격 실행 직전에 한 번 호출하며 이미 시작한 발소리와 다른 효과음을 중단하지 않는다.
    /// </summary>
    private void PlayActionSound(EnemySoundStage stage)
    {
        SoundId soundId = EnemySoundSettings.GetSound(SoundAction, stage);
        if (soundId != SoundId.None)
            Managers.Sound.PlayAttached(soundId, gameObject);
    }

    /// <summary>
    /// 전조 시작 순간에 고정된 context를 사용하여 파생 패턴의 공격 동작을 실행한다.
    /// 피해 영역이나 투사체는 context.AimPosition과 Direction을 기준으로 생성한다.
    /// </summary>
    protected abstract void PerformAttack(EnemyAttackContext context);

    /// <summary>context의 전조를 표시하기 전에 파생 패턴의 공격 방향을 준비한다. 기본 구현은 상태를 변경하지 않는다.</summary>
    protected virtual void PrepareTelegraph(EnemyAttackContext context)
    {
    }

    /// <summary>
    /// 지속 공격 중 매 프레임 context를 파생 패턴에 전달한다.
    /// 기본 구현은 상태를 변경하지 않으며 파생 클래스에서 지속 이동이나 타격을 처리한다.
    /// </summary>
    protected virtual void UpdateActiveAttack(EnemyAttackContext context)
    {
    }

    /// <summary>
    /// context와 cancelled를 사용하여 실행 종료 또는 취소에 따른 파생 공격의 자원을 정리한다.
    /// 기본 구현은 상태를 변경하지 않으며 파생 클래스에서 이동 설정 등을 복원한다.
    /// </summary>
    protected virtual void OnAttackEnded(EnemyAttackContext context, bool cancelled)
    {
    }
}
