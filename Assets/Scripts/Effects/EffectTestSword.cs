using UnityEngine;

/// <summary>
/// EffectTest 씬의 모형 검을 휘둘러 공통 Trail과 Hit를 수동 관찰하게 한다.
/// 실제 충돌이나 피해 판정을 수행하지 않는다.
/// </summary>
public sealed class EffectTestSword : MonoBehaviour
{
    [Header("씬 연결")]
    [SerializeField] private Transform _pivot;
    [SerializeField] private Transform _bladeBase;
    [SerializeField] private Transform _bladeTip;
    [SerializeField] private Transform _contactPoint;

    [Header("시연")]
    [SerializeField] private bool _autoSwing = true;
    [SerializeField] private bool _showHit = true;
    [SerializeField, Min(0.05f)] private float _swingDuration = 0.32f;
    [SerializeField, Min(0.1f)] private float _interval = 1.5f;
    [SerializeField, Range(10f, 170f)] private float _angle = 140f;
    private EffectManager _effects;
    private float _startedAt;
    private float _nextSwing;
    private int _handle;
    private bool _swinging;
    private bool _hitPlayed;

    void Start()
    {
        _effects = Game.Instance.EffectManager;
        _nextSwing = Time.time + 0.5f;
    }

    void Update()
    {
        if (!_swinging)
        {
            if (_autoSwing && Time.time >= _nextSwing)
                Swing();
            return;
        }
        float progress = Mathf.Clamp01((Time.time - _startedAt) / _swingDuration);
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        _pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-_angle * 0.5f, _angle * 0.5f, eased));
        if (_showHit && !_hitPlayed && progress >= 0.5f)
        {
            _effects.PlayHit(_contactPoint.position);
            _hitPlayed = true;
        }
        if (progress >= 1f)
        {
            _effects.EndTrail(_handle);
            _handle = 0;
            _swinging = false;
            _nextSwing = Time.time + _interval;
        }
    }

    void OnDisable()
    {
        if (_effects != null)
            _effects.StopTrail(_handle);
        _handle = 0;
        _swinging = false;
    }

    /// <summary>
    /// Inspector의 검 양 끝점으로 한 번의 베기를 시작한다.
    /// Play Mode에서 이전 Trail을 정리하고 새 요청 번호와 시작 시각을 저장한다.
    /// </summary>
    [ContextMenu("Effects/Swing")]
    public void Swing()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _effects == null)
            return;
        _effects.StopTrail(_handle);
        _pivot.localRotation = Quaternion.Euler(0f, 0f, -_angle * 0.5f);
        _handle = _effects.BeginTrail(_bladeBase, _bladeTip);
        _startedAt = Time.time;
        _swinging = true;
        _hitPlayed = false;
    }
}
