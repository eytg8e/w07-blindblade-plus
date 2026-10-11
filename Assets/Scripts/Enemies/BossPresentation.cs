using UnityEngine;

/// <summary>보스의 상태별 외형과 공간 소리·돌진 발자국을 독립적으로 표현한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossController))]
public sealed class BossPresentation : MonoBehaviour
{
    [Header("외형")]
    [SerializeField] private Renderer[] _bodyRenderers;
    private BossController _boss;
    private Vector3 _previousPosition;
    private float _stepDistanceAccumulated;
    private bool _leftStep;

    [Header("보행")]
    [SerializeField, Min(0.1f), Tooltip("걸음 사이 이동 거리(미터)")]
    private float _stepDistance = 0.8f;
    [SerializeField] private LayerMask _groundMask = 1;
    [SerializeField] private SoundId _footstepSound = SoundId.EnemyFootstep;

    [Header("돌진 기합1")]
    [SerializeField] private SoundId _chargeCry = SoundId.BossChargeCry;
    [SerializeField, Range(0.1f, 3f)] private float _chargeCryPitch = 0.85f;

    [Header("정면승부 기합2와 공격 직전음")]
    [SerializeField] private BossMeleeAttack _melee;
    [SerializeField] private SoundId _duelCry = SoundId.BossDuelCry;
    [SerializeField, Range(0.1f, 3f)] private float _duelCryPitch = 1.25f;
    [SerializeField] private SoundId _strikeCue = SoundId.EnemySwordSwing;

    [Header("스턴 비명1")]
    [SerializeField] private SoundId _stunCry = SoundId.BossStunCry;
    [SerializeField, Range(0.1f, 3f)] private float _stunCryPitch = 0.55f;

    void Awake()
    {
        _boss = GetComponent<BossController>();
    }

    void OnEnable()
    {
        _boss.StateChanged += HandleState;
        _boss.ChargePreparing += HandleChargePreparing;
        _boss.Teleported += ResetSteps;
        _melee.TelegraphStarted += HandleStrikePreparing;
        ResetSteps();
        HandleState(_boss.State);
    }

    void OnDisable()
    {
        _boss.StateChanged -= HandleState;
        _boss.ChargePreparing -= HandleChargePreparing;
        _boss.Teleported -= ResetSteps;
        _melee.TelegraphStarted -= HandleStrikePreparing;
        if (Managers.IsInitialized)
        {
            Managers.Sound.StopFor(gameObject, _chargeCry);
            Managers.Sound.StopFor(gameObject, _duelCry);
            Managers.Sound.StopFor(gameObject, _stunCry);
            Managers.Sound.StopFor(gameObject, _strikeCue);
        }
    }

    void LateUpdate()
    {
        Vector3 movement = Vector3.ProjectOnPlane(transform.position - _previousPosition, Vector3.up);
        _previousPosition = transform.position;
        bool walking = _boss.State == BossState.Roaming || IsChargeState();
        if (!walking || movement.sqrMagnitude < 0.000001f) return;
        _stepDistanceAccumulated += movement.magnitude;
        if (_stepDistanceAccumulated < _stepDistance) return;
        _stepDistanceAccumulated %= _stepDistance;
        Managers.Sound.Play(_footstepSound, transform.position, owner: gameObject, playDuringMovement: true);
        if (!IsChargeState() || !Game.IsInitialized) return;
        Vector3 direction = movement.normalized;
        Vector3 foot = transform.position + Vector3.Cross(Vector3.up, direction) * (_leftStep ? -0.15f : 0.15f);
        _leftStep = !_leftStep;
        if (Physics.Raycast(foot + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit,
            0.8f, _groundMask, QueryTriggerInteraction.Ignore))
            Game.Instance.SensoryManager.EmitFootstep(new SensoryEvent(hit.point, hit.normal, direction), _leftStep, true);
    }

    /// <summary>state가 돌진 또는 스턴일 때만 지정된 보스 외형을 표시하고 걸음 누적을 초기화한다.</summary>
    private void HandleState(BossState state)
    {
        bool visible = state == BossState.Charging || state == BossState.ChainCharge || state == BossState.Stunned;
        foreach (Renderer body in _bodyRenderers) body.enabled = visible;
        ResetSteps();
        if (Managers.IsInitialized)
        {
            Managers.Sound.StopFor(gameObject, _chargeCry);
            Managers.Sound.StopFor(gameObject, _duelCry);
            Managers.Sound.StopFor(gameObject, _stunCry);
            Managers.Sound.StopFor(gameObject, _strikeCue);
        }
        if (state == BossState.Duel)
            Managers.Sound.Play(_duelCry, transform.position, owner: gameObject, playDuringMovement: true, pitch: _duelCryPitch);
        else if (state == BossState.Stunned)
            Managers.Sound.Play(_stunCry, transform.position, owner: gameObject, playDuringMovement: true, pitch: _stunCryPitch);
    }

    /// <summary>돌진 준비 시 현재 보스 위치에서 기합1을 재생한다.</summary>
    private void HandleChargePreparing()
    {
        Managers.Sound.StopFor(gameObject, _chargeCry);
        Managers.Sound.Play(_chargeCry, transform.position, owner: gameObject, playDuringMovement: true, pitch: _chargeCryPitch);
    }

    /// <summary>context의 타격 전조가 시작되면 실제 칼날 판정 전에 공격 직전음을 재생한다.</summary>
    private void HandleStrikePreparing(EnemyAttackContext context)
    {
        Managers.Sound.StopFor(gameObject, _duelCry);
        Managers.Sound.Play(_strikeCue, transform.position, owner: gameObject, playDuringMovement: true);
    }

    /// <summary>현재 상태가 시각적 발자국을 남기는 돌진 패턴인지 반환한다.</summary>
    private bool IsChargeState()
    {
        return _boss.State == BossState.Charging || _boss.State == BossState.ChainCharge;
    }

    /// <summary>현재 위치를 걸음 시작점으로 저장하고 누적 거리를 비운다.</summary>
    private void ResetSteps()
    {
        _previousPosition = transform.position;
        _stepDistanceAccumulated = 0f;
    }
}
