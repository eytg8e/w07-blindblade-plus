using UnityEngine;

/// <summary>
/// EffectTest 씬의 모형 검을 휘둘러 검의 자동 Trail과 접촉 Hit를 관찰하게 한다.
/// 실제 충돌이나 피해 판정을 수행하지 않는다.
/// </summary>
public sealed class EffectTestSword : MonoBehaviour
{
    [Header("씬 연결")]
    [SerializeField] private Transform _pivot;
    [SerializeField] private SwordEffectEmitter _swordEffects;
    [SerializeField] private Transform _perceptionObserver;
    private SensoryManager _sensoryManager;

    [Header("시연")]
    [SerializeField] private bool _autoSwing = true;
    [SerializeField, Min(0.05f)] private float _swingDuration = 0.32f;
    [SerializeField, Min(0.1f)] private float _interval = 1.5f;
    [SerializeField, Range(10f, 170f)] private float _angle = 140f;
    private float _startedAt;
    private float _nextSwing;
    private bool _swinging;

    void Start()
    {
        _sensoryManager = Game.Instance.SensoryManager;
        _sensoryManager.SetObserver(_perceptionObserver);
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
        if (progress >= 1f)
        {
            _swordEffects.EndSwing();
            _swinging = false;
            _nextSwing = Time.time + _interval;
        }
    }

    void OnDisable()
    {
        if (_swordEffects != null)
            _swordEffects.CancelSwing();
        if (_sensoryManager != null && _sensoryManager.Observer == _perceptionObserver)
            _sensoryManager.ClearObserver();
        _swinging = false;
    }

    /// <summary>
    /// Inspector의 검 컴포넌트로 한 번의 베기를 시작한다.
    /// Play Mode에서 검 접촉 감지를 시작하고 시연 시작 시각을 저장한다.
    /// </summary>
    [ContextMenu("Effects/Swing")]
    public void Swing()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;
        _swordEffects.CancelSwing();
        _pivot.localRotation = Quaternion.Euler(0f, 0f, -_angle * 0.5f);
        if (!_swordEffects.BeginSwing())
            return;
        _startedAt = Time.time;
        _swinging = true;
    }
}
