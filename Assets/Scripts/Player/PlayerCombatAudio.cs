using UnityEngine;


/// <summary>
/// 플레이어 행동 이벤트를 검 발도와 헛스윙 및 쳐내기 소리에 연결한다.
/// 실제 적 피격음은 EnemyDamageAudio가 처리하고 모든 재생은 Managers.Sound를 사용한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[DisallowMultipleComponent]
public sealed class PlayerCombatAudio : MonoBehaviour
{
    [Header("전투 효과음")]
    [SerializeField] private SoundId _swingSound = SoundId.SwordSwing;
    [SerializeField] private SoundId _hitSound = SoundId.SwordHit;
    [SerializeField] private SoundId _unsheathSound = SoundId.SwordUnsheath;
    [SerializeField, Min(0f)] private float _volume = 0.8f;

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
    }

    /// <summary>
    /// action의 공격 단계에서 SwingType을 선택하고 미충돌 결과에 헛스윙을 한 번 재생한다.
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
        if (action.Stage == PlayerActionStage.Missed && _lastContactAction != action.ActionId)
            PlayMiss(action.ActionId);
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
    /// request의 실제 collider와 point를 사용하여 미충돌 스윙과 일반 충돌음을 구분한다.
    /// 적 피해가 확정된 경우 EnemyDamageAudio에 맡기고 나머지 접촉은 승인된 임시 충돌음을 한 번 재생한다.
    /// </summary>
    private void HandleContact(PlayerAttackRequest request, Collider collider, Vector3 point, bool enemyDamaged)
    {
        if (request.ActionId != _player.CurrentActionId || _lastContactAction == request.ActionId)
            return;
        _lastContactAction = request.ActionId;
        if (collider == null)
            PlayMiss(request.ActionId);
        else if (!enemyDamaged)
        {
            _lastHitAction = request.ActionId;
            Managers.Sound.Play(_hitSound, point, _volume, gameObject);
        }
    }

    /// <summary>
    /// result의 실제 쳐내기 접촉 위치에 사용자가 승인한 임시 충돌음을 재생한다.
    /// 피해 적용은 변경하지 않으며 이 플레이어를 소유자로 지정한다.
    /// </summary>
    private void HandleDefense(KatanaDefenseResult result)
    {
        Managers.Sound.Play(_hitSound, result.Point, result.IsParry ? 1f : _volume, gameObject);
    }

    /// <summary>
    /// actionId의 미충돌 베기에 현재 선택된 A/B/C Label을 재생 전에 적용한다.
    /// 기존 전투 시각화의 일반 공격은 판정 시점의 실제 타수를 사용한다.
    /// 동일 행동의 반복 알림을 무시하고 재생 ID를 기록한다.
    /// </summary>
    private void PlayMiss(int actionId)
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
