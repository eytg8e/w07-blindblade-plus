using System.Collections.Generic;

using UnityEngine;

/// <summary>카타나 이벤트를 복제한 Animator 클립에 연결한다. 피해 판정이나 전투 행동 종료를 호출하지 않는다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class KatanaWeaponVisual : MonoBehaviour
{
    private static readonly int ActionProgressId = Animator.StringToHash("ActionProgress");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("References")]
    [SerializeField] private KatanaCombatCoordinator _combat;
    [SerializeField] private PlayerActionDriver _actionDriver;
    [SerializeField] private Animator _animator;

    [Header("Presentation")]
    [SerializeField] private bool _showWeapon = true;
    [SerializeField, Min(0f)] private float _blendTime = 0.025f;
    [SerializeField, Range(0.01f, 0.99f)] private float _iaidoImpactProgress = 0.88461536f;
    [SerializeField, Range(0.01f, 0.99f)] private float _drawnSheatheProgress = 0.2f;
    [SerializeField, Min(0.01f)] private float _minimumCancelSheatheDuration = 0.12f;

    private readonly Dictionary<string, float> _lengths = new Dictionary<string, float>();
    private readonly Dictionary<int, string> _stateNames = new Dictionary<int, string>();
    private string _motion = "Rest";
    private float _elapsed;
    private float _duration;
    private float _impactTime;
    private float _impactProgress;
    private float _entryProgress;
    private float _flash;
    private float _lastCharge;
    private int _actionId;
    private bool _attack;
    private bool _parry;
    private bool _wasVisible;
    private Renderer[] _renderers;
    private Renderer _blade;
    private Color _bladeColor;
    private MaterialPropertyBlock _properties;
    private Transform[] _poseTargets;
    private Vector3[] _blendPositions;
    private Quaternion[] _blendRotations;
    private float _poseBlendDuration;
    private float _poseBlendElapsed;
    private bool _finishingInsertion;
    private float _insertionStart;
    private float _insertionDuration;

    public string RequestedMotion => _motion;
    public Animator Animator => _animator;
    public string AnimatorState
    {
        get
        {
            if (_animator == null || !_animator.isInitialized) return "Not initialized";
            int hash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            string name = _stateNames.TryGetValue(hash, out string value) ? value : hash.ToString();
            return _animator.IsInTransition(0) ? name + " -> " + _motion : name;
        }
    }

    void Awake()
    {
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        if (_actionDriver == null) _actionDriver = GetComponent<PlayerActionDriver>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogError("KatanaWeaponVisual requires the KatanaV2 rig Animator.", this);
            enabled = false;
            return;
        }
        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
        {
            _lengths[clip.name] = clip.length;
            _stateNames[Animator.StringToHash(clip.name)] = clip.name;
        }
        _animator.applyRootMotion = false;
        _animator.fireEvents = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _poseTargets = new[] { _animator.transform.Find("Sword"), _animator.transform.Find("Saya") };
        _blendPositions = new Vector3[_poseTargets.Length];
        _blendRotations = new Quaternion[_poseTargets.Length];
        _renderers = _animator.GetComponentsInChildren<Renderer>(true);
        _blade = _animator.transform.Find("Sword/Blade").GetComponent<Renderer>();
        _bladeColor = _blade.sharedMaterial.GetColor(BaseColorId);
        _properties = new MaterialPropertyBlock();
        _wasVisible = !_showWeapon;
    }

    void OnEnable()
    {
        if (_combat != null) _combat.EventRaised += HandleKatana;
    }

    void Start()
    {
        Play("Rest", 0f);
    }

    void OnDisable()
    {
        if (_combat != null) _combat.EventRaised -= HandleKatana;
    }

    void Update()
    {
        if (_wasVisible != _showWeapon)
        {
            foreach (Renderer renderer in _renderers) renderer.enabled = _showWeapon;
            _wasVisible = _showWeapon;
        }
        _animator.speed = _combat.Player.IsPaused ? 0f : 1f;
        if (_combat.Player.IsPaused) return;
        float dt = Time.deltaTime;
        _elapsed += dt;
        _flash = Mathf.Max(0f, _flash - dt * 5f);
        if (_combat.Player.IsGuarding)
        {
            if (_motion == "GuardEnter" || _motion == "GuardDeflect")
            {
                _animator.SetFloat(ActionProgressId, Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration)));
                if (_elapsed >= _duration) Play("GuardHold", _blendTime);
            }
            else _animator.SetFloat(ActionProgressId, 1f);
        }
        else if (_finishingInsertion)
        {
            float progress = Mathf.SmoothStep(_insertionStart, _drawnSheatheProgress,
                Mathf.Clamp01(_elapsed / _insertionDuration));
            _animator.SetFloat(ActionProgressId, progress);
            if (_elapsed >= _insertionDuration) Play("Rest", 0.08f);
        }
        else if (_motion == "Charge" || _motion == "ChargeFromDrawn")
        {
            _lastCharge = _combat.Player.ChargeProgress;
            _animator.SetFloat(ActionProgressId, _lastCharge);
        }
        else if (_attack)
        {
            float elapsed = !_parry && _actionDriver != null
                ? _actionDriver.GetPresentationElapsed(_actionId) : _elapsed;
            float progress = elapsed < _impactTime
                ? Mathf.Lerp(_entryProgress, _impactProgress, elapsed / Mathf.Max(0.001f, _impactTime))
                : Mathf.Lerp(_impactProgress, 1f, (elapsed - _impactTime) / Mathf.Max(0.001f, _duration - _impactTime));
            _animator.SetFloat(ActionProgressId, progress);
        }
        else if (_motion.StartsWith("Sheathe") && _motion != "SheathedHold")
            _animator.SetFloat(ActionProgressId, Mathf.Clamp01(_elapsed / _combat.SheatheDuration));
        else if (_motion == "ChargeRelease" && _elapsed >= _duration)
            Play("Rest", _blendTime);

        _blade.GetPropertyBlock(_properties, 0);
        float tint = Mathf.Max(_flash, _combat.Player.IsChargingReady ? 0.1f : 0f);
        _properties.SetColor(BaseColorId, Color.Lerp(_bladeColor, Color.cyan, tint));
        _blade.SetPropertyBlock(_properties, 0);
    }

    void LateUpdate()
    {
        if (_poseBlendDuration > 0f)
        {
            if (!_combat.Player.IsPaused) _poseBlendElapsed += Time.deltaTime;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_poseBlendElapsed / _poseBlendDuration));
            for (int i = 0; i < _poseTargets.Length; i++)
            {
                Transform target = _poseTargets[i];
                target.localPosition = Vector3.Lerp(_blendPositions[i], target.localPosition, weight);
                target.localRotation = Quaternion.Slerp(_blendRotations[i], target.localRotation, weight);
            }
            if (_poseBlendElapsed >= _poseBlendDuration) _poseBlendDuration = 0f;
        }
    }

/// <summary>e의 전투 이벤트를 발도 공격 클립과 성공 피드백으로 변환한다. 피해 판정은 호출하지 않는다.</summary>
    private void HandleKatana(KatanaEvent e)
    {
        switch (e.Stage)
        {
            case KatanaStage.NormalStarted:
                BeginStrike("IaidoDoubleSlash", e);
                break;
            case KatanaStage.ParrySucceeded:
                _flash = 1f;
                Play("GuardDeflect", _blendTime);
                _duration = 0.16f;
                break;
            case KatanaStage.GuardStarted:
                _attack = false;
                _parry = false;
                _finishingInsertion = false;
                Play("GuardEnter", _blendTime);
                _duration = 0.12f;
                break;
            case KatanaStage.GuardSucceeded:
                _flash = 0.6f;
                Play("GuardDeflect", _blendTime);
                _duration = 0.16f;
                break;
            case KatanaStage.GuardEnded:
                Play("GuardHold", _blendTime);
                _animator.SetFloat(ActionProgressId, 1f);
                break;
            case KatanaStage.ParryMotionEnded:
            case KatanaStage.DrawnWaiting:
                _attack = false;
                _animator.SetFloat(ActionProgressId, 1f);
                break;
            case KatanaStage.StrikeEnded:
                if (!_parry)
                {
                    _attack = false;
                    _animator.SetFloat(ActionProgressId, 1f);
                }
                break;
            case KatanaStage.SheatheStarted:
                string sheath = _motion == "IaidoDoubleSlash" ? "SheatheIaido" : _motion == "Normal2" ? "Sheathe2" : "Sheathe1";
                Play(sheath, _blendTime);
                _animator.SetFloat(ActionProgressId, 0f);
                break;
            case KatanaStage.SheatheEnded:
                if (!_combat.Player.IsCharging) Play("Rest", _blendTime);
                break;
            case KatanaStage.ChargeStarted:
                _lastCharge = 0f;
                Play(e.WasDrawn ? "ChargeFromDrawn" : "Charge", 0.08f);
                _animator.SetFloat(ActionProgressId, 0f);
                break;
            case KatanaStage.ChargeReleased:
                ReleaseCharge();
                break;
            case KatanaStage.DashStarted:
                Play("SheathedHold", _blendTime);
                break;
            case KatanaStage.SlashStarted:
                BeginStrike("IaidoDoubleSlash", e);
                break;
            case KatanaStage.Died:
                Play("Dead", _blendTime);
                break;
        }
    }

    /// <summary>발도 클립 name을 기존 Iaido 적중·종료 시간에 맞추고 진행도를 초기화한다.</summary>
    private void BeginStrike(string name, KatanaEvent e)
    {
        Play(name, _blendTime);
        _attack = true;
        _parry = false;
        _actionId = e.ActionId;
        _impactProgress = _iaidoImpactProgress;
        _entryProgress = 0f;
        if (_actionDriver != null)
            _actionDriver.GetPresentationTiming(PlayerAttackKind.IaidoFinisher, out _impactTime, out _duration);
        else
            _impactTime = _duration * _impactProgress;
        _animator.SetFloat(ActionProgressId, _entryProgress);
    }

    /// <summary>충전 취소 시 현재 진행도를 사용한다. 발도 상태에서 시작한 납도가 덜 끝났다면 삽입 표현만 마저 재생한다.</summary>
    private void ReleaseCharge()
    {
        bool fromDrawn = _motion == "ChargeFromDrawn";
        if (fromDrawn && _lastCharge < _drawnSheatheProgress)
        {
            _finishingInsertion = true;
            _insertionStart = _lastCharge;
            _insertionDuration = Mathf.Max(_minimumCancelSheatheDuration,
                (_drawnSheatheProgress - _lastCharge) * _duration);
            _elapsed = 0f;
            return;
        }
        float readyProgress = fromDrawn
            ? Mathf.InverseLerp(_drawnSheatheProgress, 1f, _lastCharge) : _lastCharge;
        Play("ChargeRelease", 0.08f, 1f - readyProgress);
    }

    /// <summary>name 상태로 전환하고 표현 시계를 초기화한다. blend 동안 직전 표시 자세에서 보간하며 normalizedStart는 클립 진입 위치다.</summary>
    private void Play(string name, float blend, float normalizedStart = 0f)
    {
        for (int i = 0; i < _poseTargets.Length; i++)
        {
            _blendPositions[i] = _poseTargets[i].localPosition;
            _blendRotations[i] = _poseTargets[i].localRotation;
        }
        _poseBlendDuration = blend;
        _poseBlendElapsed = 0f;
        _finishingInsertion = false;
        _motion = name;
        _attack = false;
        _duration = _lengths.TryGetValue(name, out float length) ? Mathf.Max(length, 0.001f) : 0.1f;
        _elapsed = Mathf.Clamp01(normalizedStart) * _duration;
        int hash = Animator.StringToHash("Base Layer." + name);
        // 공용 ActionProgress 초기화가 이전 상태까지 되감지 않도록 현재 표시 자세를 별도로 보간한다.
        _animator.Play(hash, 0, normalizedStart);
    }
}
