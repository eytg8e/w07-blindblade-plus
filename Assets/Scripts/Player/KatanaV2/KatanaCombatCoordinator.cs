using System;

using UnityEngine;

using BlindBlade.Combat;

namespace BlindBlade.Player.Katana
{
    public enum KatanaWeaponState { Sheathed, Drawn, Sheathing }
    public enum KatanaStage
    {
        NormalStarted, StrikeEnded, ParryAttempted, ParryWindowClosed, ParrySucceeded, ParryFailed, ParryMotionEnded,
        DrawnWaiting, SheatheStarted, SheatheEnded, ChargeStarted, ChargeReady,
        ChargeReleased, DashStarted, SlashStarted, Died, RequestRejected
    }

    public readonly struct KatanaEvent
    {
        public KatanaStage Stage { get; }
        public int Strike { get; }
        public int ActionId { get; }
        public bool WasDrawn { get; }
        public string Detail { get; }

        /// <summary>단계, 행동 ID, 이전 발도 여부와 설명을 표현용 이벤트에 저장한다. Strike는 호환을 위해 항상 1타다.</summary>
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

    /// <summary>기존 플레이어 판정에 단일 발도 공격, 공격 직전 쳐내기 창, 납도 흐름을 연결한다.</summary>
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

        [Header("Sheathe")]
        [SerializeField, Min(0.01f)] private float _sheatheDuration = 0.3f;

        private PlayerTestActionDriver _actionDriver;
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
        public PlayerController Player => _player;
        public KatanaWeaponState WeaponState => _weaponState;
        public int Strike => _strike;
        public bool IsParryWindowOpen => _parryOpen && _clock < _parryUntil
            && GetAttackElapsed() < _attackParryHitTime;
        public bool IsParryMotion => _parryMotion;
        public float SheatheDuration => _sheatheDuration;
        public float ParryRemaining => IsParryWindowOpen ? Mathf.Max(0f, _parryUntil - _clock) : 0f;
        public bool CanStartAction => isActiveAndEnabled && _player != null
            && Time.timeScale > 0f && _player.CurrentState == PlayerActionState.Free
            && _weaponState != KatanaWeaponState.Sheathing && !_parryOpen && !_parryMotion;

        void Awake()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
            _actionDriver = GetComponent<PlayerTestActionDriver>();
        }

        void OnEnable()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
            _player.ActionEvent += HandlePlayerAction;
        }

        void OnDisable()
        {
            if (_player != null)
                _player.ActionEvent -= HandlePlayerAction;
            _parryOpen = false;
            _parryMotion = false;
            ResetChain();
        }

        void Update()
        {
            if (_player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
                return;

            _clock += Time.deltaTime;
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

        /// <summary>자유 상태에서 단일 발도 공격을 시작하고 공격 판정 전까지 쳐내기 창을 연다. 입력을 수락했는지 반환한다.</summary>
        public bool RequestAttack()
        {
            if (!CanStartAction)
                return Reject("Attack: current action blocks input");

            _attackWasDrawn = _weaponState != KatanaWeaponState.Sheathed;
            _strike = 1;
            _waiting = false;
            _normalRequested = true;
            bool accepted = _player.RequestNormalAttack();
            _normalRequested = false;
            return accepted;
        }

        /// <summary>기존 일반공격 호출자를 단일 발도 공격 흐름으로 연결한다. 연계 타수는 선택하지 않는다.</summary>
        public bool RequestNormalAttack() => RequestAttack();

        /// <summary>기존 입력 호출자를 단일 공격·공격 직전 쳐내기 흐름으로 연결한다.</summary>
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

        /// <summary>현재 발도 공격의 적중 판정 전에 호출한다. 유효한 전방 근접 타격이면 수신 피해를 막고 플레이어 피해를 취소한다.</summary>
        public bool TryParryIncoming(DamageContext incoming, bool eligible)
        {
            if (!isActiveAndEnabled || incoming.Amount <= 0f || _player.IsPaused || Time.timeScale <= 0f
                || _player.CurrentState != PlayerActionState.NormalAttack || !_parryOpen)
                return false;

            if (!IsParryWindowOpen)
            {
                CloseAttackParryWindow();
                return false;
            }
            if (!eligible)
            {
                FailParry("Attack type or direction not parryable");
                return false;
            }

            int actionId = _player.CurrentActionId;
            float elapsed = GetAttackElapsed();
            GetIaidoTiming(out _, out float actionDuration);
            bool wasDrawn = _weaponState != KatanaWeaponState.Sheathed;
            _strike = 1;
            _waiting = false;
            _parryOpen = false;
            _parryMotion = true;
            _parryMotionUntil = _clock + Mathf.Max(0f, actionDuration - elapsed);
            _weaponState = KatanaWeaponState.Drawn;
            Publish(KatanaStage.ParrySucceeded, incoming.PatternId, wasDrawn);
            ParrySucceeded?.Invoke(new KatanaParryResult(incoming, _strike));
            _player.CancelNormalAttack(actionId);
            return true;
        }

        /// <summary>상위 시스템의 paused를 기존 Controller에 전달한다. 카타나 타이머도 Update에서 함께 정지한다.</summary>
        public void SetPaused(bool paused) => _player.SetPaused(paused);

        /// <summary>입력 소유권을 잃으면 진행 중인 공격의 쳐내기 창을 닫는다. 다시 입력을 예약하지 않는다.</summary>
        public void CancelParryAttempt()
        {
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

        /// <summary>발도 적중 시각을 쳐내기 창의 종료점으로 사용하고 공격 시각화 이벤트를 발행한다.</summary>
        private void OpenAttackParryWindow()
        {
            GetIaidoTiming(out _attackParryHitTime, out _);
            _attackStartedAt = _clock;
            _parryUntil = _clock + _attackParryHitTime;
            _parryOpen = true;
            Publish(KatanaStage.ParryAttempted, "LMB Iaido attack: parry until slash impact");
        }

        /// <summary>정상 공격 판정 프레임에 도달하면 쳐내기 창만 닫고 진행 중인 발도 공격은 유지한다.</summary>
        private void CloseAttackParryWindow()
        {
            if (!_parryOpen)
                return;

            _parryOpen = false;
            Publish(KatanaStage.ParryWindowClosed, "No incoming attack before slash impact");
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

        /// <summary>기존 플레이어 이벤트를 단일 발도, 회복 대기, 납도 표현에 연결한다.</summary>
        private void HandlePlayerAction(PlayerActionEvent e)
        {
            if (e.State == PlayerActionState.Paused)
                return;
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
                    }
                    _strike = 1;
                    _actionDriver?.UseIaidoTimingForNextNormalAttack();
                    _weaponState = KatanaWeaponState.Drawn;
                    Publish(KatanaStage.NormalStarted, "Single Iaido attack", _attackWasDrawn);
                    OpenAttackParryWindow();
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
                        _waitAfterRecovery = true;
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
    }
}
