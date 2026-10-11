using UnityEngine;


/// <summary>
/// 플레이어 베기·방어·피격을 FMOD 사운드에 연결한다.
/// 실제 적 피격음은 EnemyDamageAudio가 처리하고 모든 재생은 Managers.Sound를 사용한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[DisallowMultipleComponent]
public sealed class PlayerCombatAudio : MonoBehaviour
{
    [Header("전투 효과음")]
    [SerializeField] private SoundId _swingSound = SoundId.SwordSwing;
    [SerializeField] private SoundId _hitSound = SoundId.SwordClash;
    [SerializeField] private SoundId _unsheathSound = SoundId.SwordUnsheath;
    [SerializeField, Min(0f)] private float _volume = 0.8f;

    [Header("방어·피격 신호")]
    [SerializeField] private SoundId _parrySound = SoundId.ParryClash;
    [SerializeField] private SoundId _damageSound = SoundId.PlayerDamaged;
    [SerializeField, Min(0f)] private float _parryVolume = 1.1f;
    [SerializeField, Range(0.25f, 4f)] private float _guardPitch = 0.85f;
    [SerializeField, Range(0.25f, 4f)] private float _parryPitch = 1.25f;
    private PlayerHealth _health;
    private PlayerActionDriver _timing;
    private int _pendingSwingAction = -1;
    private float _swingBeginTime;

    [Header("Combat Contact")]
    [SerializeField] private EnemyCombatService _contactService;

    [Header("SwingType Labels")]
    [SerializeField] private string _firstSwingLabel = "A";
    [SerializeField] private string _secondSwingLabel = "B";
    [SerializeField] private string _iaidoSwingLabel = "C";
    private PlayerController _player;
    private KatanaCombatCoordinator _katana;
    private string _swingLabel;
    private int _lastSwingAction = -1;
    private int _lastHitAction = -1;
    private int _lastContactAction = -1;
    private bool _introPlayed;
    private bool _isPrimary;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _katana = GetComponent<KatanaCombatCoordinator>();
        _health = GetComponent<PlayerHealth>();
        _timing = GetComponent<PlayerActionDriver>();
        // 기존 씬의 적 피격음 override도 금속 접촉음으로 연결한다.
        if (_hitSound == SoundId.SwordHit) _hitSound = SoundId.SwordClash;
        _isPrimary = GetComponent<PlayerCombatAudio>() == this;
        if (!_isPrimary) enabled = false;
    }

    void OnEnable()
    {
        if (!_isPrimary) return;
        _player.ActionEvent += HandleAction;
        _lastSwingAction = -1;
        _lastHitAction = -1;
        _lastContactAction = -1;
        _introPlayed = false;
        _pendingSwingAction = -1;
        _health.DamageReceived += HandleDamage;
        if (_contactService != null)
            _contactService.AttackContactResolved += HandleContact;
        if (_katana != null && _katana.isActiveAndEnabled)
        {
            _katana.EventRaised += HandleKatana;
            _katana.DefenseSucceeded += HandleDefense;
        }
    }

    void OnDisable()
    {
        if (!_isPrimary) return;
        _player.ActionEvent -= HandleAction;
        _health.DamageReceived -= HandleDamage;
        _pendingSwingAction = -1;
        if (_contactService != null)
            _contactService.AttackContactResolved -= HandleContact;
        if (_katana != null)
        {
            _katana.EventRaised -= HandleKatana;
            _katana.DefenseSucceeded -= HandleDefense;
        }
        if (Managers.IsInitialized)
            Managers.Sound.StopFor(gameObject);
    }

    void Update()
    {
        if (!_introPlayed)
        {
            _introPlayed = true;
            PlayUnsheath();
        }
        if (_pendingSwingAction < 0 || _player.IsPaused || Time.deltaTime <= 0f) return;
        if (_pendingSwingAction != _player.CurrentActionId
            || (_player.CurrentState != PlayerActionState.NormalAttack && _player.CurrentState != PlayerActionState.IaidoSlash))
        {
            _pendingSwingAction = -1;
            return;
        }
        if (_timing.GetPresentationElapsed(_pendingSwingAction) < _swingBeginTime) return;
        PlaySwing(_pendingSwingAction);
        _pendingSwingAction = -1;
    }

    /// <summary>
    /// action의 공격 단계에서 SwingType을 선택하고 실제 베기 시작 시각에 스윙을 예약한다.
    /// 발도 베기 시작에는 검을 뽑는 소리를 연결하며 명중음은 적 피해 사건에 맡긴다.
    /// </summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if (action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
        {
            int strike = _katana != null && _katana.isActiveAndEnabled ? _katana.Strike
                :  1;
            _swingLabel = strike > 1 ? _secondSwingLabel : _firstSwingLabel;
        }
        if (action.Stage == PlayerActionStage.SlashStarted)
        {
            _swingLabel = _iaidoSwingLabel;
            if (_katana == null || !_katana.isActiveAndEnabled)
                PlayUnsheath();
        }
        if ((action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted)
        {
            _pendingSwingAction = action.ActionId;
            _timing.GetStrikeWindow(action.State == PlayerActionState.IaidoSlash ? PlayerAttackKind.IaidoFinisher : PlayerAttackKind.Normal,
                out _swingBeginTime, out _);
        }
        if (action.Stage == PlayerActionStage.ActionEnded && action.ActionId == _pendingSwingAction)
            _pendingSwingAction = -1;
        if (_contactService == null && action.Stage == PlayerActionStage.HitConfirmed && _lastHitAction != action.ActionId)
        {
            _lastHitAction = action.ActionId;
            Managers.Sound.PlayAttached(_hitSound, gameObject, _volume);
        }
    }

    /// <summary>
    /// action의 납도 여부와 공격 종류로 실제 검을 꺼내는 시점에 소리를 재생한다.
    /// 일반 공격은 납도 상태에서 시작할 때만 처리하고 발도 베기는 항상 검을 꺼낸다.
    /// </summary>
    private void HandleKatana(KatanaEvent action)
    {
        if (((action.Stage == KatanaStage.NormalStarted || action.Stage == KatanaStage.ParrySucceeded) && !action.WasDrawn)
            || action.Stage == KatanaStage.SlashStarted)
            PlayUnsheath();
    }

    /// <summary>
    /// request의 실제 collider와 point에 일반 충돌음을 한 번 재생한다.
    /// 적 피해음은 EnemyDamageAudio에 맡기고 헛스윙에는 추가 결과음을 재생하지 않는다.
    /// </summary>
    private void HandleContact(PlayerAttackRequest request, Collider collider, Vector3 point, bool enemyDamaged)
    {
        if (request.ActionId != _player.CurrentActionId || _lastContactAction == request.ActionId)
            return;
        _lastContactAction = request.ActionId;
        if (collider != null && !enemyDamaged)
        {
            _lastHitAction = request.ActionId;
            Managers.Sound.Play(_hitSound, point, _volume, gameObject);
        }
    }

    /// <summary>
    /// result의 방어 종류와 실제 접촉 위치로 가드·패링의 음량·음높이를 선택한다.
    /// 전용 금속음을 재생하며 피해와 반격 기회는 변경하지 않는다.
    /// </summary>
    private void HandleDefense(KatanaDefenseResult result)
    {
        Managers.Sound.Play(result.IsParry ? _parrySound : _hitSound, result.Point,
            result.IsParry ? _parryVolume : _volume, gameObject, pitch: result.IsParry ? _parryPitch : _guardPitch);
    }

    /// <summary>
    /// context의 실제 플레이어 피해에 접촉 위치의 피격음을 재생한다.
    /// 피해 수신 사건만 구독하므로 가드·패링에 피격음을 중복 재생하지 않는다.
    /// </summary>
    private void HandleDamage(DamageContext context)
    {
        Managers.Sound.Play(_damageSound, context.Point, _volume, gameObject);
    }

    /// <summary>
    /// actionId의 베기 시작에 현재 선택된 A/B/C Label로 스윙을 재생한다.
    /// 기존 전투 시각화의 일반 공격은 판정 시점의 실제 타수를 사용한다.
    /// 동일 행동의 반복 알림을 무시하고 재생 ID를 기록한다.
    /// </summary>
    private void PlaySwing(int actionId)
    {
        if (_lastSwingAction == actionId)
            return;
        if ((_katana == null || !_katana.isActiveAndEnabled) && _player.CurrentState == PlayerActionState.NormalAttack)
            _swingLabel = _secondSwingLabel;
        _lastSwingAction = actionId;
        Managers.Sound.PlayAttachedWithLabel(_swingSound, gameObject, "SwingType", _swingLabel ?? _firstSwingLabel, _volume);
    }

    /// <summary>
    /// Inspector의 발도 사운드 ID와 음량으로 플레이어의 검 뽑기 소리를 한 번 재생한다.
    /// FMOD 인스턴스는 매니저가 생성하고 자연 종료 시 해제한다.
    /// </summary>
    private void PlayUnsheath()
    {
        Managers.Sound.PlayAttached(_unsheathSound, gameObject, _volume);
    }
}
