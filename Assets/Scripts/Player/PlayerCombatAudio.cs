using UnityEngine;

/// <summary>공격 행동 단계에 맞춰 검 휘두르기와 실제 명중 소리를 재생한다.</summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerCombatAudio : MonoBehaviour
{
    [Header("전투 효과음")]
    [SerializeField] private SoundId _swingSound = SoundId.SwordSwing;
    [SerializeField] private SoundId _hitSound = SoundId.SwordHit;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
    private PlayerController _player;
    private int _lastSwingAction = -1;
    private int _lastHitAction = -1;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
    }

    /// <summary>action의 일반 공격 시작·발도 베기·명중 확정 단계마다 해당 소리를 행동당 한 번 재생한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        bool swing = (action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted;
        if (swing && _lastSwingAction != action.ActionId)
        {
            _lastSwingAction = action.ActionId;
            Managers.Sound.PlayAttached(_swingSound, gameObject, _volume);
        }
        if (action.Stage == PlayerActionStage.HitConfirmed && _lastHitAction != action.ActionId)
        {
            _lastHitAction = action.ActionId;
            Managers.Sound.PlayAttached(_hitSound, gameObject, _volume);
        }
    }
}
