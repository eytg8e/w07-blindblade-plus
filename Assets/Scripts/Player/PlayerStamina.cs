using System;

using UnityEngine;

/// <summary>플레이어 공격·방어 사건으로 스태미나를 소모하고 대기 후 회복한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerStamina : MonoBehaviour
{
    [Header("스태미나")]
    [SerializeField, Min(1f)] private float _maximum = 60f;
    [SerializeField, Min(0f)] private float _attackCost = 20f;
    [SerializeField, Range(0f, 1f), Tooltip("방어 성공 시 최대 스태미나에서 소모하는 비율") ]
    private float _guardCostRatio = 0.5f;
    [SerializeField, Min(0f), Tooltip("초당 회복량")]
    private float _recoveryRate = 40f;
    [SerializeField, Min(0f), Tooltip("마지막 소모 후 회복 대기 시간(초)")]
    private float _recoveryDelay = 1f;
    private PlayerController _player;
    private float _current;
    private float _recoverAt;
    private int _lastAttackId = -1;

    public float Current => _current;
    public float Maximum => _maximum;
    public bool CanAct => _current > 0f;
    public event Action<float, float> Changed;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _current = _maximum;
    }

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
    }

    void Update()
    {
        if (_player.IsPaused || _player.CurrentState == PlayerActionState.Dead
            || _player.IsGuarding || Time.time < _recoverAt || _current >= _maximum) return;
        if (_player.CurrentState == PlayerActionState.NormalAttack
            || _player.CurrentState == PlayerActionState.IaidoDash
            || _player.CurrentState == PlayerActionState.IaidoSlash) return;
        float next = Mathf.Min(_maximum, _current + _recoveryRate * Time.deltaTime);
        if (next == _current) return;
        _current = next;
        Changed?.Invoke(_current, _maximum);
    }

    /// <summary>action의 공격 시작은 행동 ID당 한 번, 방어 성공은 접촉마다 스태미나를 소모한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if ((action.Stage == PlayerActionStage.AttackStarted || action.Stage == PlayerActionStage.DashStarted)
            && action.ActionId != _lastAttackId)
        {
            _lastAttackId = action.ActionId;
            Consume(_attackCost);
        }
        else if (action.Stage == PlayerActionStage.GuardSucceeded)
            Consume(_maximum * _guardCostRatio);
    }

    /// <summary>amount를 현재 스태미나에서 차감하고 회복 대기와 변경 사건을 갱신한다.</summary>
    private void Consume(float amount)
    {
        _current = Mathf.Max(0f, _current - amount);
        _recoverAt = Time.time + _recoveryDelay;
        Changed?.Invoke(_current, _maximum);
    }
}
