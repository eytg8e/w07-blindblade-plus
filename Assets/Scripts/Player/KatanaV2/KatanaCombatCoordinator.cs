using System;

using UnityEngine;

using BlindBlade.Combat;

namespace BlindBlade.Player.Katana
{
    public enum KatanaWeaponState { Sheathed, Drawn, Sheathing }
    public enum KatanaChainKind { None, Normal, Parry }
    public enum KatanaStage
    {
        NormalStarted, StrikeEnded, ParryAttempted, ParrySucceeded, ParryFailed, ParryMotionEnded,
        DrawnWaiting, SheatheStarted, SheatheEnded, ChargeStarted, ChargeReady,
        ChargeReleased, DashStarted, SlashStarted, Died, RequestRejected, ChainExpired
    }

    public readonly struct KatanaEvent
    {
        public KatanaStage Stage { get; }
        public int Strike { get; }
        public int ActionId { get; }
        public bool WasDrawn { get; }
        public string Detail { get; }

        /// <summary>단계, 연계 순번, 행동 ID, 이전 발도 여부와 설명을 표현용 이벤트에 저장한다.</summary>
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

        /// <summary>차단한 원래 피해 문맥과 성공 순번을 저장한다. 반격 피해나 적 상태는 변경하지 않는다.</summary>
        public KatanaParryResult(DamageContext incoming, int strike)
        {
            Incoming = incoming;
            Strike = strike;
        }
    }

    /// <summary>기존 플레이어 판정 앞에서 카타나 명령, 2연계, 납도 및 쳐내기 시간을 관리한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(-50)]
    public sealed class KatanaCombatCoordinator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController _player;

        [Header("Normal chain - after recovery")]
        [SerializeField, Min(0f)] private float _normalChainWindow = 0.6f;
        [SerializeField, Min(0f)] private float _normalSheatheDelay = 0.8f;

        [Header("Parry - scaled seconds")]
        [SerializeField, Min(0.01f)] private float _parryWindow = 0.15f;
        [SerializeField, Min(0.01f)] private float _parryRetryInterval = 0.3f;
        [SerializeField, Min(0.01f)] private float _parryMotionDuration = 0.4f;
        [SerializeField, Min(0f)] private float _parryChainWindow = 3f;

        [Header("Sheathe")]
        [SerializeField, Min(0.01f)] private float _sheatheDuration = 0.3f;

        private KatanaWeaponState _weaponState;
        private KatanaChainKind _chainKind;
        private int _strike;
        private float _clock;
        private float _parryUntil;
        private float _retryAt;
        private float _parryMotionUntil;
        private float _sheatheUntil;
        private float _waitStarted;
        private bool _parryOpen;
        private bool _parryMotion;
        private bool _waiting;
        private bool _chainExpired;
        private bool _waitAfterRecovery;
        private bool _normalRequested;
        private bool _attackWasDrawn;

        public event Action<KatanaEvent> EventRaised;
        public event Action<KatanaParryResult> ParrySucceeded;
        public PlayerController Player => _player;
        public KatanaWeaponState WeaponState => _weaponState;
        public KatanaChainKind ChainKind => _chainKind;
        public int Strike => _strike;
        public bool IsParryWindowOpen => _parryOpen && _clock < _parryUntil;
        public bool IsParryMotion => _parryMotion;
        public float ParryMotionDuration => _parryMotionDuration;
        public float SheatheDuration => _sheatheDuration;
        public float ParryRemaining => IsParryWindowOpen ? _parryUntil - _clock : 0f;
        public bool CanStartAction => isActiveAndEnabled && _player != null
            && Time.timeScale > 0f && _player.CurrentState == PlayerActionState.Free && !_parryOpen && !_parryMotion;

        void Awake()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
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
            if (_parryOpen && _clock >= _parryUntil)
                FailParry("Timing window expired");
            if (_parryMotion && _clock >= _parryMotionUntil)
            {
                _parryMotion = false;
                Publish(KatanaStage.ParryMotionEnded);
                if (_strike == 2)
                    BeginSheathe();
                else
                    BeginWaiting();
            }
            if (_weaponState == KatanaWeaponState.Sheathing && _clock >= _sheatheUntil)
            {
                _weaponState = KatanaWeaponState.Sheathed;
                Publish(KatanaStage.SheatheEnded);
            }
            if (!_waiting || _player.CurrentState != PlayerActionState.Free || _parryMotion)
                return;
            float age = _clock - _waitStarted;
            float window = _chainKind == KatanaChainKind.Parry ? _parryChainWindow : _normalChainWindow;
            if (!_chainExpired && age > window)
            {
                _chainExpired = true;
                Publish(KatanaStage.ChainExpired, "Next accepted strike starts at Normal1");
            }
            float delay = _chainKind == KatanaChainKind.Parry ? _parryChainWindow : Mathf.Max(_normalSheatheDelay, window);
            if (!_parryOpen && age >= delay)
                BeginSheathe();
        }

        /// <summary>value를 기존 이동 명령으로 전달한다. 충전·회복·사망 이동 규칙은 기존 Controller가 적용한다.</summary>
        public void SetMovementInput(Vector2 value) => _player.SetMovementInput(value);

        /// <summary>delta를 기존 시점 명령으로 전달한다. 사망과 일시정지 제한을 유지한다.</summary>
        public void AddLookInput(Vector2 delta) => _player.AddLookInput(delta);

        /// <summary>일반 상태에서 1·2타를 선택하고 기존 공격을 요청한다. 거부된 입력은 예약하지 않고 false를 반환한다.</summary>
        public bool RequestNormalAttack()
        {
            if (!CanStartAction)
                return Reject("Normal: current action blocks input");
            _attackWasDrawn = _weaponState != KatanaWeaponState.Sheathed;
            _strike = CanContinue(KatanaChainKind.Normal) ? 2 : 1;
            _chainKind = KatanaChainKind.Normal;
            _waiting = false;
            _normalRequested = true;
            bool accepted = _player.RequestNormalAttack();
            _normalRequested = false;
            return accepted;
        }

        /// <summary>행동이 허용될 때 기존 충전을 시작한다. 기존 충전 시간과 이동 제한을 유지하며 수락 여부를 반환한다.</summary>
        public bool BeginCharge()
        {
            return CanStartAction ? _player.BeginCharge() : Reject("Charge: current action blocks input");
        }

        /// <summary>기존 충전을 해제한다. 완료·조기 해제의 기존 회복 시간은 Controller가 적용한다.</summary>
        public void EndCharge() => _player.EndCharge();

        /// <summary>충전 완료 상태에서 기존 발도를 요청한다. 타격 판정과 결과별 회복은 변경하지 않는다.</summary>
        public bool RequestIaidoAttack()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || _parryOpen || _parryMotion || !_player.IsChargingReady || _player.IsPaused)
                return Reject("Iaido: charge not ready");
            return _player.RequestIaidoAttack();
        }

        /// <summary>새 버튼 누름에 짧은 쳐내기 창을 연다. 유지 입력이나 재시도 제한 중 요청은 false를 반환한다.</summary>
        public bool RequestParry()
        {
            if (!CanStartAction || _clock < _retryAt)
                return Reject("Parry: action or retry interval blocks input");
            if (_chainKind != KatanaChainKind.Parry)
                ResetChain();
            _parryOpen = true;
            _parryUntil = _clock + _parryWindow;
            _retryAt = _clock + Mathf.Max(_parryRetryInterval, _parryWindow);
            Publish(KatanaStage.ParryAttempted);
            return true;
        }

        /// <summary>피해 적용 직전에 호출한다. 유효한 창과 eligible이 모두 참이면 이번 타격만 소비하고 성공 이벤트를 발행한다.</summary>
        public bool TryParryIncoming(DamageContext incoming, bool eligible)
        {
            if (!isActiveAndEnabled || incoming.Amount <= 0f || _player.IsPaused || Time.timeScale <= 0f || _player.CurrentState != PlayerActionState.Free)
                return false;
            if (!IsParryWindowOpen || !eligible)
            {
                if (_parryOpen)
                    FailParry(eligible ? "Hit outside timing window" : "Attack type or direction not parryable");
                return false;
            }
            bool wasDrawn = _weaponState != KatanaWeaponState.Sheathed;
            _strike = CanContinue(KatanaChainKind.Parry) ? 2 : 1;
            _chainKind = KatanaChainKind.Parry;
            _waiting = false;
            _parryOpen = false;
            _parryMotion = true;
            _parryMotionUntil = _clock + _parryMotionDuration;
            _weaponState = KatanaWeaponState.Drawn;
            Publish(KatanaStage.ParrySucceeded, incoming.PatternId, wasDrawn);
            ParrySucceeded?.Invoke(new KatanaParryResult(incoming, _strike));
            return true;
        }

        /// <summary>상위 시스템의 paused를 기존 Controller에 전달한다. 카타나 타이머도 Update에서 함께 정지한다.</summary>
        public void SetPaused(bool paused) => _player.SetPaused(paused);

        /// <summary>입력 소유권을 잃으면 열린 쳐내기 창을 취소한다. 누른 버튼을 복귀 시 다시 시도하지 않는다.</summary>
        public void CancelParryAttempt()
        {
            if (_parryOpen)
                FailParry("Input focus lost");
        }

        /// <summary>kind가 이전 성공 종류와 같고 대기 시간이 남아 있으면 2타 연결 가능 여부를 반환한다.</summary>
        private bool CanContinue(KatanaChainKind kind)
        {
            float window = kind == KatanaChainKind.Parry ? _parryChainWindow : _normalChainWindow;
            return _waiting && _chainKind == kind && _strike == 1 && _clock - _waitStarted <= window;
        }

        /// <summary>첫 동작의 회복 또는 성공 표현 종료부터 연계·납도 대기를 시작한다.</summary>
        private void BeginWaiting()
        {
            _waiting = true;
            _chainExpired = false;
            _waitStarted = _clock;
            Publish(KatanaStage.DrawnWaiting);
        }

        /// <summary>발도된 검의 납도 표현을 시작하고 연계를 초기화한다. 추가 전투 후딜은 만들지 않는다.</summary>
        private void BeginSheathe()
        {
            _weaponState = KatanaWeaponState.Sheathing;
            _sheatheUntil = _clock + _sheatheDuration;
            Publish(KatanaStage.SheatheStarted);
            ResetChain();
        }

        /// <summary>연계와 회복 후 대기 예약만 비운다. 기존 Controller의 행동 상태는 변경하지 않는다.</summary>
        private void ResetChain()
        {
            _chainKind = KatanaChainKind.None;
            _strike = 0;
            _waiting = false;
            _waitAfterRecovery = false;
            _chainExpired = false;
        }

        /// <summary>열린 창을 한 번 닫고 reason을 실패 이벤트로 전달한다. 연계 성공 횟수는 증가시키지 않는다.</summary>
        private void FailParry(string reason)
        {
            _parryOpen = false;
            Publish(KatanaStage.ParryFailed, reason);
            if (!_waiting && _weaponState == KatanaWeaponState.Drawn)
                BeginSheathe();
        }

        /// <summary>reason으로 입력 거부를 알리고 false를 반환한다. 입력 큐는 생성하지 않는다.</summary>
        private bool Reject(string reason)
        {
            Publish(KatanaStage.RequestRejected, reason);
            return false;
        }

        /// <summary>기존 전투 이벤트를 새 표현·납도 흐름에 연결한다. 기존 피해·충전·회복 수치는 변경하지 않는다.</summary>
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
                        _chainKind = KatanaChainKind.Normal;
                        _strike = 1;
                    }
                    _weaponState = KatanaWeaponState.Drawn;
                    Publish(KatanaStage.NormalStarted, _strike == 2 ? "Chain accepted" : "First strike", _attackWasDrawn);
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
                    if (e.State == PlayerActionState.NormalAttack && _strike == 1)
                        _waitAfterRecovery = true;
                    else if (_weaponState == KatanaWeaponState.Drawn)
                        BeginSheathe();
                    else if (e.Outcome == PlayerAttackOutcome.WallBlocked)
                        Publish(KatanaStage.ChargeReleased, "Wall blocked");
                    break;
                case PlayerActionStage.RecoveryEnded:
                case PlayerActionStage.StateChanged:
                    if (_waitAfterRecovery && e.State == PlayerActionState.Free)
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
