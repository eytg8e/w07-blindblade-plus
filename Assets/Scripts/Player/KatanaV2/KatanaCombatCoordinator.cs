using System;

using UnityEngine;


public enum KatanaWeaponState { Sheathed, Drawn, Sheathing }
public enum KatanaStage
{
    NormalStarted, StrikeEnded, ParryAttempted, ParryWindowClosed, ParrySucceeded, ParryFailed, ParryMotionEnded,
    DrawnWaiting, SheatheStarted, SheatheEnded, ChargeStarted, ChargeReady,
    ChargeReleased, DashStarted, SlashStarted, Died, RequestRejected, GuardStarted, GuardEnded, GuardSucceeded
}

public readonly struct KatanaEvent
{
    public KatanaStage Stage { get; }
    public int Strike { get; }
    public int ActionId { get; }
    public bool WasDrawn { get; }
    public string Detail { get; }

    /// <summary>단계, 행동 ID, 이전 발도 여부와 설명을 표현용 이벤트에 저장한다. Strike는 일반 베기의 1·2타를 구분한다.</summary>
    public KatanaEvent(KatanaStage stage, int strike, int actionId, bool wasDrawn, string detail)
    {
        Stage = stage;
        Strike = strike;
        ActionId = actionId;
        WasDrawn = wasDrawn;
        Detail = detail;
    }
}

public readonly struct KatanaParryResult
{
    public DamageContext Incoming { get; }
    public int Strike { get; }

    /// <summary>차단한 원래 피해 문맥과 단일 발도 행동 순번을 저장한다. 반격 피해나 적 상태는 변경하지 않는다.</summary>
    public KatanaParryResult(DamageContext incoming, int strike)
    {
        Incoming = incoming;
        Strike = strike;
    }
}

/// <summary>플레이어의 2타 연계, 유지 가드·정확한 패링, 납도 흐름과 방어 피드백을 연결한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
[DefaultExecutionOrder(-50)]
public sealed class KatanaCombatCoordinator : MonoBehaviour
{
    private const float FALLBACK_IAIDO_HIT_TIME = 0.42f;
    private const float FALLBACK_IAIDO_DURATION = 0.72f;

    [Header("References")]
    [SerializeField] private PlayerController _player;

    [Header("Attack / sheathe")]
    [SerializeField, Min(0f)] private float _normalSheatheDelay = 0.8f;

    [Header("일반 공격 2타 연계")]
    [SerializeField, Min(0f)] private float _comboBufferWindow = 0.2f;
    [SerializeField, Min(0f)] private float _comboFollowThrough = 0.06f;
    [SerializeField, Min(0f)] private float _comboResetDelay = 0.65f;
    private bool _queuedAttack;
    private int _queuedActionId;
    private float _comboExpiresAt;
    private bool _chainTransition;

    [Header("방어")]
    [SerializeField, Min(0.01f)] private float _parryWindow = 0.18f;
    [SerializeField, Min(0f)] private float _blockInputRecovery = 0.18f;
    private PlayerMotor _motor;
    private PlayerHealth _health;
    private float _defenseReadyUntil;

    [Header("Sheathe")]
    [SerializeField, Min(0.01f)] private float _sheatheDuration = 0.3f;

    private PlayerActionDriver _actionDriver;
    private KatanaWeaponState _weaponState;
    private int _strike;
    private float _clock;
    private float _parryUntil;
    private float _parryMotionUntil;
    private float _sheatheUntil;
    private float _waitStarted;
    private float _attackStartedAt;
    private float _attackParryHitTime;
    private bool _parryOpen;
    private bool _parryMotion;
    private bool _waiting;
    private bool _waitAfterRecovery;
    private bool _normalRequested;
    private bool _attackWasDrawn;

    public event Action<KatanaEvent> EventRaised;
    public event Action<KatanaParryResult> ParrySucceeded;
    public event Action<KatanaDefenseResult> DefenseSucceeded;
    public PlayerController Player => _player;
    public KatanaWeaponState WeaponState => _weaponState;
    public int Strike => _strike;
    public bool HasBufferedAttack => _queuedAttack;
    public bool IsParryWindowOpen => _parryOpen && Time.time < _parryUntil
        && _player.IsGuarding;
    public bool IsParryMotion => _parryMotion;
    public float SheatheDuration => _sheatheDuration;
    public float ParryRemaining => IsParryWindowOpen ? Mathf.Max(0f, _parryUntil - Time.time) : 0f;
    public bool CanStartAction => isActiveAndEnabled && _player != null
        && Time.timeScale > 0f && _player.CurrentState == PlayerActionState.Free
        && _weaponState != KatanaWeaponState.Sheathing && !_parryOpen && !_parryMotion && Time.time >= _defenseReadyUntil;

    void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        _actionDriver = GetComponent<PlayerActionDriver>();
        _motor = GetComponent<PlayerMotor>();
        _health = GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        _player.ActionEvent += HandlePlayerAction;
        _health.DamageReceived += HandleDamage;
    }

    void OnDisable()
    {
        if (_player != null)
            _player.ActionEvent -= HandlePlayerAction;
        _health.DamageReceived -= HandleDamage;
        _parryOpen = false;
        _parryMotion = false;
        ResetChain();
    }

    void Update()
    {
        if (_player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
            return;

        _clock += Time.deltaTime;
        if (_queuedAttack && _player.CurrentState == PlayerActionState.NormalAttack
            && _queuedActionId == _player.CurrentActionId && GetAttackElapsed() >= GetChainStart())
            TryStartQueuedAttack();
        if (_player.CurrentState == PlayerActionState.Free && _clock >= _comboExpiresAt) _strike = 0;
        if (_parryOpen && !IsParryWindowOpen)
            CloseAttackParryWindow();

        if (_parryMotion && _clock >= _parryMotionUntil)
        {
            _parryMotion = false;
            Publish(KatanaStage.ParryMotionEnded);
            if (_player.CurrentState == PlayerActionState.Free)
            {
                _waitAfterRecovery = false;
                BeginWaiting();
            }
        }

        if (_weaponState == KatanaWeaponState.Sheathing && _clock >= _sheatheUntil)
        {
            _weaponState = KatanaWeaponState.Sheathed;
            Publish(KatanaStage.SheatheEnded);
        }

        if (!_waiting || _player.CurrentState != PlayerActionState.Free || _parryMotion)
            return;
        if (!_parryOpen && _clock - _waitStarted >= _normalSheatheDelay)
            BeginSheathe();
    }

    /// <summary>value를 기존 이동 명령으로 전달한다. 충전·회복·사망 이동 규칙은 기존 Controller가 적용한다.</summary>
    public void SetMovementInput(Vector2 value) => _player.SetMovementInput(value);

    /// <summary>delta를 기존 시점 명령으로 전달한다. 사망과 일시정지 제한을 유지한다.</summary>
    public void AddLookInput(Vector2 delta) => _player.AddLookInput(delta);

    /// <summary>자유 상태에서는 1·2타를 시작하고 첫 베기의 연계 입력 구간에서는 다음 클릭 하나를 예약한다. 수락 여부를 반환한다.</summary>
    public bool RequestAttack()
    {
        if (!isActiveAndEnabled || _player.IsPaused || Time.timeScale <= 0f)
            return Reject("Attack: input unavailable");
        if (_player.CurrentState == PlayerActionState.NormalAttack && _strike == 1)
        {
            if (_queuedAttack || GetAttackElapsed() < GetChainStart() - _comboBufferWindow)
                return Reject("Attack: chain input unavailable");
            _queuedAttack = true;
            _queuedActionId = _player.CurrentActionId;
            if (GetAttackElapsed() >= GetChainStart()) TryStartQueuedAttack();
            return true;
        }
        if (!CanStartAction) return Reject("Attack: current action blocks input");
        int strike = _strike == 1 && _clock < _comboExpiresAt ? 2 : 1;
        _attackWasDrawn = _weaponState != KatanaWeaponState.Sheathed;
        _strike = strike;
        _waiting = false;
        _queuedAttack = false;
        _normalRequested = true;
        bool accepted = _player.RequestNormalAttack();
        _normalRequested = false;
        return accepted;
    }

    /// <summary>현재 일반 공격의 타격 종료와 최소 후속 동작 시간으로 다음 베기를 시작할 시각을 반환한다.</summary>
    private float GetChainStart()
    {
        _actionDriver.GetStrikeWindow(PlayerAttackKind.Normal, out _, out float end);
        _actionDriver.GetPresentationTiming(PlayerAttackKind.Normal, out _, out float duration);
        return Mathf.Min(duration, end + _comboFollowThrough);
    }

    /// <summary>예약된 첫 공격 ID의 판정이 끝났으면 회복 진입 없이 2타로 연결한다. 실패 시 예약을 유지한다.</summary>
    private void TryStartQueuedAttack()
    {
        _attackWasDrawn = true;
        _normalRequested = true;
        _chainTransition = true;
        bool accepted = _player.TryChainNormalAttack(_queuedActionId);
        _chainTransition = false;
        _normalRequested = false;
        if (accepted) { _queuedAttack = false; _waiting = false; _waitAfterRecovery = false; }
    }

    /// <summary>context의 실제 피격 알림을 받으면 연계 예약과 다음 타수 유지 시간을 취소한다. 진행 중인 공격은 Controller의 피격 처리를 따른다.</summary>
    private void HandleDamage(DamageContext context)
    {
        _queuedAttack = false;
        _comboExpiresAt = 0f;
    }

    /// <summary>기존 일반공격 호출자를 2타 연계 입력 흐름으로 연결한다. 수락 여부를 반환한다.</summary>
    public bool RequestNormalAttack() => RequestAttack();

    /// <summary>기존 입력 호출자를 2타 베기 흐름으로 연결한다. 방어 패링 창은 열지 않으며 수락 여부를 반환한다.</summary>
    public bool RequestNormalAttackWithParry() => RequestAttack();

    /// <summary>행동이 허용될 때 기존 충전을 시작한다. 기존 충전 시간과 이동 제한을 유지하며 수락 여부를 반환한다.</summary>
    public bool BeginCharge()
    {
        return CanStartAction ? _player.BeginCharge() : Reject("Charge: current action blocks input");
    }

    /// <summary>기존 충전을 해제한다. 완료·조기 해제의 기존 회복 시간은 Controller가 적용한다.</summary>
    public void EndCharge() => _player.EndCharge();

    /// <summary>충전 완료 상태에서 발도를 요청한다. 기존 충전 조건과 공격 판정을 유지한다.</summary>
    public bool RequestIaidoAttack()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || _parryOpen || _parryMotion || !_player.IsChargingReady || _player.IsPaused)
            return Reject("Iaido: charge not ready");
        return _player.RequestIaidoAttack();
    }

    /// <summary>incoming과 eligible을 현재 가드의 패링 시간에 검사한다. 유효한 정확한 패링의 방어 성공 여부를 반환한다.</summary>
    public bool TryParryIncoming(DamageContext incoming, bool eligible)
    {
        return IsParryWindowOpen && TryDefendIncoming(incoming, eligible);
    }

    /// <summary>상위 시스템의 paused를 기존 Controller에 전달한다. 카타나 타이머도 Update에서 함께 정지한다.</summary>
    public void SetPaused(bool paused) => _player.SetPaused(paused);

    /// <summary>입력 소유권 상실 시 연계 예약·타수 유지 시간을 지우고 진행 중인 패링 창을 닫는다. 현재 공격은 강제 종료하지 않는다.</summary>
    public void CancelParryAttempt()
    {
        _queuedAttack = false;
        _comboExpiresAt = 0f;
        if (_parryOpen)
            FailParry("Input focus lost");
    }

    /// <summary>발도 동작의 적중 시각과 종료 시각을 읽고 드라이버가 없으면 기본값을 사용한다.</summary>
    private void GetIaidoTiming(out float hitTime, out float duration)
    {
        if (_actionDriver != null)
        {
            _actionDriver.GetPresentationTiming(PlayerAttackKind.IaidoFinisher, out hitTime, out duration);
            return;
        }

        hitTime = FALLBACK_IAIDO_HIT_TIME;
        duration = FALLBACK_IAIDO_DURATION;
    }

    /// <summary>현재 공격의 경과 시간을 읽고 테스트 드라이버가 없으면 Coordinator 시계로 계산한다.</summary>
    private float GetAttackElapsed()
    {
        return _actionDriver != null
            ? _actionDriver.GetPresentationElapsed(_player.CurrentActionId)
            : Mathf.Max(0f, _clock - _attackStartedAt);
    }

    /// <summary>현재 클럭과 _parryWindow로 가드 시작 직후의 패링 창을 열고 시각화 이벤트를 발행한다.</summary>
    private void OpenDefenseWindow()
    {
        _parryUntil = Time.time + _parryWindow;
        _parryOpen = true;
        Publish(KatanaStage.ParryAttempted, "Guard started: timed parry window");
    }

    /// <summary>패링 시간이 지나면 정확한 패링 창만 닫는다. 우클릭 유지 가드는 계속 허용한다.</summary>
    private void CloseAttackParryWindow()
    {
        if (!_parryOpen)
            return;

        _parryOpen = false;
        Publish(KatanaStage.ParryWindowClosed, "Timed parry window ended; held guard remains");
    }

    /// <summary>공격 회복이 끝난 뒤 단일 행동의 납도 대기를 시작하고 디버그 이벤트를 발행한다.</summary>
    private void BeginWaiting()
    {
        _waiting = true;
        _waitStarted = _clock;
        Publish(KatanaStage.DrawnWaiting);
    }

    /// <summary>발도된 검의 납도 표현을 시작하고 다음 공격 대기를 초기화한다.</summary>
    private void BeginSheathe()
    {
        _weaponState = KatanaWeaponState.Sheathing;
        _sheatheUntil = _clock + _sheatheDuration;
        Publish(KatanaStage.SheatheStarted);
        ResetChain();
    }

    /// <summary>마지막 행동 표시와 납도 대기 예약을 비운다. 기존 Controller의 행동 상태는 변경하지 않는다.</summary>
    private void ResetChain()
    {
        _strike = 0;
        _queuedAttack = false;
        _comboExpiresAt = 0f;
        _waiting = false;
        _waitAfterRecovery = false;
    }

    /// <summary>공격 중 쳐내기 창을 한 번 닫고 입력 취소 또는 판정 실패를 이벤트로 전달한다.</summary>
    private void FailParry(string reason)
    {
        if (!_parryOpen)
            return;

        _parryOpen = false;
        Publish(KatanaStage.ParryFailed, reason);
    }

    /// <summary>reason으로 입력 거부를 알리고 false를 반환한다. 입력 큐는 생성하지 않는다.</summary>
    private bool Reject(string reason)
    {
        Publish(KatanaStage.RequestRejected, reason);
        return false;
    }

    /// <summary>e의 일반 공격·방어·회복 이벤트로 2타 연계와 납도 상태를 변경한다. 중단 상태에서는 예약을 취소한다.</summary>
    private void HandlePlayerAction(PlayerActionEvent e)
    {
        if (e.State == PlayerActionState.Paused)
        {
            _queuedAttack = false;
            _comboExpiresAt = 0f;
            return;
        }
        if (e.State == PlayerActionState.Dead)
        {
            _parryOpen = false;
            _parryMotion = false;
            ResetChain();
            Publish(KatanaStage.Died);
            return;
        }

        switch (e.Stage)
        {
            case PlayerActionStage.AttackStarted:
                if (e.State != PlayerActionState.NormalAttack) break;
                if (!_normalRequested)
                {
                    _attackWasDrawn = _weaponState != KatanaWeaponState.Sheathed;
                    ResetChain();
                    _strike = 1;
                }
                if (_chainTransition) _strike = 2;
                _attackStartedAt = _clock;
                _weaponState = KatanaWeaponState.Drawn;
                Publish(KatanaStage.NormalStarted, _strike == 2 ? "Normal chain 2" : "Normal chain 1", _attackWasDrawn);
                _parryOpen = false;
                break;
            case PlayerActionStage.GuardStarted:
                _queuedAttack = false;
                _comboExpiresAt = 0f;
                _weaponState = KatanaWeaponState.Drawn;
                _waiting = false;
                Publish(KatanaStage.GuardStarted);
                break;
            case PlayerActionStage.GuardEnded:
                _parryOpen = false;
                Publish(KatanaStage.GuardEnded);
                BeginWaiting();
                break;
            case PlayerActionStage.GuardSucceeded:
                Publish(KatanaStage.GuardSucceeded);
                break;
            case PlayerActionStage.ChargeStarted:
                bool drawn = _weaponState != KatanaWeaponState.Sheathed;
                ResetChain();
                _weaponState = drawn ? KatanaWeaponState.Sheathing : KatanaWeaponState.Sheathed;
                _sheatheUntil = _clock + 0.2f;
                Publish(KatanaStage.ChargeStarted, "", drawn);
                break;
            case PlayerActionStage.ChargeReady:
                Publish(KatanaStage.ChargeReady);
                break;
            case PlayerActionStage.ChargeCancelled:
            case PlayerActionStage.ChargeEnded:
                _weaponState = KatanaWeaponState.Sheathed;
                Publish(KatanaStage.ChargeReleased, e.Stage.ToString());
                break;
            case PlayerActionStage.DashStarted:
                ResetChain();
                _weaponState = KatanaWeaponState.Sheathed;
                Publish(KatanaStage.DashStarted);
                break;
            case PlayerActionStage.SlashStarted:
                _weaponState = KatanaWeaponState.Drawn;
                Publish(KatanaStage.SlashStarted);
                break;
            case PlayerActionStage.ActionEnded:
                Publish(KatanaStage.StrikeEnded);
                if (e.State == PlayerActionState.NormalAttack)
                {
                    if (!_chainTransition)
                    {
                        _queuedAttack = false;
                        _comboExpiresAt = _strike == 1 && e.Outcome != PlayerAttackOutcome.None
                            ? _clock + _comboResetDelay : 0f;
                        _waitAfterRecovery = true;
                    }
                }
                else if (_weaponState == KatanaWeaponState.Drawn)
                    BeginSheathe();
                else if (e.Outcome == PlayerAttackOutcome.WallBlocked)
                    Publish(KatanaStage.ChargeReleased, "Wall blocked");
                break;
            case PlayerActionStage.RecoveryEnded:
            case PlayerActionStage.StateChanged:
                if (_waitAfterRecovery && e.State == PlayerActionState.Free && !_parryMotion)
                {
                    _waitAfterRecovery = false;
                    BeginWaiting();
                }
                break;
        }
    }

    /// <summary>stage와 선택 데이터를 현재 행동 ID와 함께 외부 표현·디버그 구독자에게 전달한다.</summary>
    private void Publish(KatanaStage stage, string detail = "", bool wasDrawn = false)
    {
        EventRaised?.Invoke(new KatanaEvent(stage, _strike, _player.CurrentActionId, wasDrawn, detail));
    }

    /// <summary>자유 상태에서 가드를 시작하고 짧은 패링 창을 연다. 방어 입력 수락 여부를 반환한다.</summary>
    public bool BeginDefense()
    {
        if (!isActiveAndEnabled || _player.CurrentState != PlayerActionState.Free || _player.IsPaused
            || Time.timeScale <= 0f || Time.time < _defenseReadyUntil) return false;
        _waiting = false;
        if (!_player.BeginGuard()) return false;
        OpenDefenseWindow();
        return true;
    }

    /// <summary>유지 가드와 패링 창을 종료한다. 기존 행동 상태와 납도 대기 흐름으로 복귀한다.</summary>
    public void EndDefense()
    {
        _parryOpen = false;
        _player.EndGuard();
    }

    /// <summary>
    /// incoming이 eligible인 전방 근접 접촉이고 가드 중이면 피해를 차단한다.
    /// 패링 시간에 따라 적 회복·검 반동·카메라 및 공통 방어 피드백을 발생시키고 성공 여부를 반환한다.
    /// </summary>
    public bool TryDefendIncoming(DamageContext incoming, bool eligible)
    {
        if (!isActiveAndEnabled || !eligible || !_player.IsGuarding || _player.IsPaused
            || incoming.Amount <= 0f || Time.timeScale <= 0f) return false;
        bool perfect = IsParryWindowOpen;
        _defenseReadyUntil = perfect ? Time.time : Time.time + _blockInputRecovery;
        Vector3 point = _player.GetComponent<EnemyCombatService>().GetGuardContactPoint(incoming.Point);
        _parryOpen = false;
        _waiting = false;
        _weaponState = KatanaWeaponState.Drawn;
        _player.NotifyGuardSuccess();
        _motor.AddCombatRecoil(incoming.Direction, perfect ? 0.9f : 0.6f);
        foreach (EnemyAttackBehaviour pattern in incoming.Instigator.GetComponents<EnemyAttackBehaviour>())
            if (pattern.PatternId == incoming.PatternId && pattern is EnemyMeleeAttack melee) melee.Deflect(perfect, point);
        DefenseSucceeded?.Invoke(new KatanaDefenseResult(incoming, point, perfect));
        if (perfect)
        {
            Publish(KatanaStage.ParrySucceeded, incoming.PatternId, true);
            ParrySucceeded?.Invoke(new KatanaParryResult(incoming, 1));
        }
        return true;
    }

}

/// <summary>방어 접촉의 원래 피해, 검 앞 접촉점, 정확한 패링 여부를 표현 담당자에게 전달한다.</summary>
public readonly struct KatanaDefenseResult
{
    public DamageContext Incoming { get; }
    public Vector3 Point { get; }
    public bool IsParry { get; }

    /// <summary>incoming·point·isParry를 저장한다. 피해나 공격 상태를 직접 변경하지 않는다.</summary>
    public KatanaDefenseResult(DamageContext incoming, Vector3 point, bool isParry)
    {
        Incoming = incoming;
        Point = point;
        IsParry = isParry;
    }
}
