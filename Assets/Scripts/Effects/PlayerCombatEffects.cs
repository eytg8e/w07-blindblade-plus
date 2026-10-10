using UnityEngine;

using BlindBlade.Enemies;
using BlindBlade.Player.Katana;

/// <summary>
/// 기존 플레이어 행동·접촉 이벤트를 구독해 검 접촉 감지·타격·혈흔을 요청한다.
/// 전투 판정과 사운드는 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class PlayerCombatEffects : MonoBehaviour
{
    [Header("플레이어 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerTestActionDriver _timing;
    [SerializeField] private EnemyCombatService _contacts;
    [SerializeField] private SwordEffectEmitter _physicalContacts;
    [SerializeField] private KatanaCombatCoordinator _katana;

    [Header("베기 표시 구간")]
    [SerializeField, Range(0f, 1f)] private float _normalWindupFraction = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _iaidoWindupFraction = 0.8f;
    [SerializeField, Range(0.5f, 1f)] private float _followThroughFraction = 0.85f;

    private EffectManager _effects;
    private int _actionId;
    private int _lastContactAction = -1;
    private float _beginTime;
    private float _endTime;
    private bool _tracking;
    private bool _started;

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
        if (_contacts != null && _physicalContacts == null)
            _contacts.AttackContactResolved += HandleContact;
        _katana.ParrySucceeded += HandleParry;
        _lastContactAction = -1;
    }

    void Start()
    {
        _effects = Game.Instance.EffectManager;
        if (!_effects.IsReady)
            _effects.Initialize();
        _effects.SetOutlineVfxEnabled(false);
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
        if (_contacts != null && _physicalContacts == null)
            _contacts.AttackContactResolved -= HandleContact;
        _katana.ParrySucceeded -= HandleParry;
        CancelContactDetection();
    }

    void LateUpdate()
    {
        if (!_tracking || _player.IsPaused || Time.deltaTime <= 0f)
            return;
        if (_effects == null || !_effects.IsReady || _player.CurrentActionId != _actionId)
        {
            CancelContactDetection();
            return;
        }
        float elapsed = _timing.GetPresentationElapsed(_actionId);
        if (elapsed >= _endTime)
        {
            EndContactDetection();
            return;
        }
        if (!_started && elapsed >= _beginTime)
        {
            if (_physicalContacts != null)
                _started = _physicalContacts.BeginSwing();
        }
    }

    /// <summary>
    /// action의 일반·발도 베기 시작과 종료·사망을 관찰한다.
    /// action의 표현 타이밍으로 접촉 감지 구간을 예약하고 정상 종료 또는 취소한다.
    /// </summary>
    private void HandleAction(PlayerActionEvent action)
    {
        bool normal = action.Stage == PlayerActionStage.AttackStarted
            && action.State == PlayerActionState.NormalAttack;
        bool iaido = action.Stage == PlayerActionStage.SlashStarted;
        if (normal || iaido)
        {
            CancelContactDetection();
            _actionId = action.ActionId;
            _timing.GetPresentationTiming(iaido ? PlayerAttackKind.IaidoFinisher : PlayerAttackKind.Normal,
                out float impactTime, out float duration);
            _beginTime = impactTime * (iaido ? _iaidoWindupFraction : _normalWindupFraction);
            _endTime = Mathf.Max(impactTime, duration * _followThroughFraction);
            _tracking = _physicalContacts != null;
        }
        else if (action.Stage == PlayerActionStage.ActionEnded && action.ActionId == _actionId)
            EndContactDetection();
        else if (action.Stage == PlayerActionStage.StateChanged && action.State == PlayerActionState.Dead)
            CancelContactDetection();
    }

    /// <summary>
    /// request의 실제 collider와 월드 point로 공용 타격을 요청한다.
    /// 적 Collider에는 혈흔을 추가하고 같은 행동의 접촉 알림은 한 번만 처리한다.
    /// </summary>
    private void HandleContact(PlayerAttackRequest request, Collider collider, Vector3 point, bool enemyDamaged)
    {
        if (request.ActionId != _player.CurrentActionId || _lastContactAction == request.ActionId)
            return;
        _lastContactAction = request.ActionId;
        if (collider == null || _effects == null || !_effects.IsReady)
            return;
        _effects.PlayHit(point);
        if (enemyDamaged || collider.GetComponentInParent<EnemyHealth>(true) != null)
            _effects.Play(EffectId.BloodHit, point);
    }

    /// <summary>
    /// result의 실제 쳐내기 접촉점에 공용 타격 효과를 요청한다.
    /// 가드 성공을 표현하며 혈흔이나 피해를 추가하지 않는다.
    /// </summary>
    private void HandleParry(KatanaParryResult result)
    {
        if (_effects != null && _effects.IsReady)
            _effects.PlayHit(result.Incoming.Point);
    }

    /// <summary>
    /// 입력 없이 연결된 검의 접촉 감지를 정상 종료한다.
    /// 현재 공격의 예약 상태를 해제하며 재생된 타격 효과는 자연 종료한다.
    /// </summary>
    private void EndContactDetection()
    {
        if (_physicalContacts != null)
            _physicalContacts.EndSwing();
        _tracking = false;
        _started = false;
    }

    /// <summary>
    /// 입력 없이 연결된 검의 접촉 감지와 예약을 즉시 취소한다.
    /// 비활성화·사망·새 공격 시작 시 이전 감지 상태를 남기지 않는다.
    /// </summary>
    private void CancelContactDetection()
    {
        if (_physicalContacts != null)
            _physicalContacts.CancelSwing();
        _tracking = false;
        _started = false;
    }
}
