using System.Collections.Generic;

using UnityEngine;

/// <summary>플레이어 이벤트를 편집 가능한 검 Animator 클립에 연결한다. 전투 판정은 기존 행동 드라이버가 담당한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerWeaponDebugVisual : MonoBehaviour
{
    private static readonly Color BladeColor = new Color(0.72f, 0.79f, 0.88f);
    private static readonly Color ChargedBladeColor = new Color(0.28f, 0.82f, 1f);
    private static readonly int ActionProgressId = Animator.StringToHash("ActionProgress");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("References and display")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private Camera _viewCamera;
    [SerializeField] private PlayerTestActionDriver _actionDriver;
    [SerializeField] private GameObject _rigPrefab;
    [SerializeField] private Animator _animator;
    [SerializeField] private bool _showWeapon = true;

    [Header("Clip transitions")]
    [SerializeField] private bool _playIntroOnStart = true;
    [SerializeField, Min(0.01f)] private float _poseTransitionTime = 0.16f;
    [SerializeField, Range(0f, 0.1f)] private float _actionBlendTime = 0.025f;
    [SerializeField, Min(0.1f)] private float _comboResetDelay = 1.2f;
    [SerializeField, Range(0f, 0.3f)] private float _readyTintStrength = 0.10f;

    [Header("Impact position in editable clips (normalized)")]
    [SerializeField, Range(0.01f, 0.99f)] private float _normalImpactProgress = 0.375f;
    [SerializeField, Range(0.01f, 0.99f)] private float _iaidoImpactProgress = 0.5833333f;

    private readonly Dictionary<string, float> _clipLengths = new Dictionary<string, float>();
    private MaterialPropertyBlock _bladeProperties;
    private Renderer _bladeRenderer;
    private Renderer[] _renderers;
    private string _motion = "Rest";
    private float _motionElapsed;
    private float _motionDuration;
    private float _attackDuration;
    private float _impactTime;
    private float _impactProgress;
    private float _lastChargeProgress;
    private float _comboWaitElapsed;
    private float _guardFlash;
    private int _nextNormalStrike;
    private int _normalStrike;
    private int _visualActionId;
    private bool _started;
    private bool _wasPaused;
    private bool _resyncAfterPause;
    private bool _visible;
    private bool _ownsRig;

    public float GuardTransitionTime => ClipLength("GuardEnter");
    public int CurrentNormalStrike => _normalStrike + 1;
    public bool IsPlayingIntro => _motion == "Intro";
    public bool IsAtGuardPose => _motion == "GuardHold" && !_animator.IsInTransition(0);
    public bool IsAtRestPose => _motion == "Rest" && !_animator.IsInTransition(0);
    public bool HasGuardSuccessColor => _guardFlash > 0.05f;

    void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_viewCamera == null)
            _viewCamera = GetComponentInChildren<Camera>(true);
        if (_actionDriver == null)
            _actionDriver = GetComponent<PlayerTestActionDriver>();
        if (_animator == null && _rigPrefab != null && _viewCamera != null)
        {
            GameObject rig = Instantiate(_rigPrefab, _viewCamera.transform, false);
            rig.name = _rigPrefab.name;
            _animator = rig.GetComponent<Animator>();
            _ownsRig = true;
        }
        if (_player == null || _animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerWeaponDebugVisual requires PlayerController and PlayerWeaponRig Animator.", this);
            enabled = false;
            return;
        }

        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            _clipLengths[clip.name] = clip.length;
        _animator.applyRootMotion = false;
        _animator.fireEvents = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _renderers = _animator.GetComponentsInChildren<Renderer>(true);
        _bladeRenderer = _animator.transform.Find("Sword/Blade").GetComponent<Renderer>();
        _bladeProperties = new MaterialPropertyBlock();
        _visible = !_showWeapon;
        ApplyVisibility();
    }

    void OnEnable()
    {
        if (_player == null || _animator == null)
            return;
        _player.ActionEvent += OnPlayerAction;
        _animator.gameObject.SetActive(true);
        if (_started)
        {
            if (_player.IsPaused)
                _resyncAfterPause = true;
            else
                ApplyCurrentPose();
        }
    }

    void Start()
    {
        _started = true;
        if (_playIntroOnStart && _player.CurrentState == PlayerActionState.Free)
            PlayMotion("Intro", 0f);
        else
            ApplyCurrentPose();
    }

    void OnDisable()
    {
        if (_player != null)
            _player.ActionEvent -= OnPlayerAction;
        if (_animator != null)
            _animator.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_ownsRig && _animator != null)
            Destroy(_animator.gameObject);
    }

    void Update()
    {
        ApplyVisibility();
        _animator.speed = _player.IsPaused ? 0f : 1f;
        if (_player.IsPaused)
            return;
        if (_resyncAfterPause)
        {
            _resyncAfterPause = false;
            ApplyCurrentPose();
        }

        float delta = Time.deltaTime;
        _motionElapsed += delta;
        _guardFlash = Mathf.Max(0f, _guardFlash - delta / ClipLength("GuardDeflect"));
        if (_player.CurrentState == PlayerActionState.Free)
        {
            _comboWaitElapsed += delta;
            if (_comboWaitElapsed > _comboResetDelay)
                _nextNormalStrike = 0;
        }

        if (_motion == "Charge")
        {
            _lastChargeProgress = _player.ChargeProgress;
            _animator.SetFloat(ActionProgressId, _lastChargeProgress);
        }
        else if (IsAttackMotion())
        {
            float elapsed = _actionDriver != null
                ? _actionDriver.GetPresentationElapsed(_visualActionId) : _motionElapsed;
            _animator.SetFloat(ActionProgressId, AttackProgress(elapsed));
        }
        else if (_motionElapsed >= _motionDuration)
        {
            if (_motion == "Intro" || _motion == "ChargeRelease")
                PlayMotion("Rest", _actionBlendTime);
            else if (_motion == "GuardEnter" || _motion == "GuardDeflect")
                PlayMotion("GuardHold", _actionBlendTime);
        }
        ApplyBladeTint();
    }

    /// <summary>기존 행동 이벤트로 클립과 순번을 선택한다. 공격 판정이나 행동 종료를 직접 요청하지 않는다.</summary>
    private void OnPlayerAction(PlayerActionEvent actionEvent)
    {
        if (actionEvent.State == PlayerActionState.Paused)
        {
            _wasPaused = true;
            _animator.speed = 0f;
            return;
        }
        if (_wasPaused)
        {
            _wasPaused = false;
            _animator.speed = 1f;
            if (actionEvent.State != PlayerActionState.Dead)
                return;
        }

        switch (actionEvent.Stage)
        {
            case PlayerActionStage.ChargeStarted:
                _nextNormalStrike = 0;
                _lastChargeProgress = 0f;
                _guardFlash = 0f;
                _animator.SetFloat(ActionProgressId, 0f);
                PlayMotion("Charge", _actionBlendTime);
                break;
            case PlayerActionStage.ChargeReady:
                _lastChargeProgress = 1f;
                _animator.SetFloat(ActionProgressId, 1f);
                break;
            case PlayerActionStage.ChargeCancelled:
            case PlayerActionStage.ChargeEnded:
                PlayMotion("ChargeRelease", _actionBlendTime, 1f - _lastChargeProgress);
                break;
            case PlayerActionStage.GuardStarted:
                _nextNormalStrike = 0;
                _guardFlash = 0f;
                PlayMotion("GuardEnter", _actionBlendTime);
                break;
            case PlayerActionStage.GuardSucceeded:
                _guardFlash = 1f;
                PlayMotion("GuardDeflect", _actionBlendTime);
                ApplyBladeTint();
                break;
            case PlayerActionStage.GuardEnded:
                MoveToRest();
                break;
            case PlayerActionStage.AttackStarted:
                if (actionEvent.State == PlayerActionState.NormalAttack)
                {
                    _normalStrike = _nextNormalStrike;
                    _nextNormalStrike = (_nextNormalStrike + 1) % 3;
                    _comboWaitElapsed = 0f;
                    BeginAttack("Normal" + CurrentNormalStrike, PlayerAttackKind.Normal, actionEvent.ActionId);
                }
                break;
            case PlayerActionStage.DashStarted:
                _nextNormalStrike = 0;
                PlayMotion("SheathedHold", _actionBlendTime);
                break;
            case PlayerActionStage.SlashStarted:
                BeginAttack("IaidoDoubleSlash", PlayerAttackKind.IaidoFinisher, actionEvent.ActionId);
                break;
            case PlayerActionStage.ActionEnded:
                if (IsAttackMotion())
                    _animator.SetFloat(ActionProgressId, 1f);
                break;
            case PlayerActionStage.RecoveryStarted:
                if (_motion != "ChargeRelease")
                    MoveToRest();
                break;
            case PlayerActionStage.RecoveryEnded:
                if (_motion != "ChargeRelease" && _motion != "Rest")
                    MoveToRest();
                _comboWaitElapsed = 0f;
                break;
            case PlayerActionStage.StateChanged:
                if (actionEvent.State == PlayerActionState.Dead)
                {
                    _nextNormalStrike = 0;
                    _guardFlash = 0f;
                    PlayMotion("Dead", _actionBlendTime);
                }
                else if (actionEvent.State == PlayerActionState.Free && _motion != "ChargeRelease")
                    MoveToRest();
                break;
        }
    }

    /// <summary>재활성화 시 현재 전투 상태로 클립을 복원한다. 시작 연출은 반복하지 않는다.</summary>
    private void ApplyCurrentPose()
    {
        switch (_player.CurrentState)
        {
            case PlayerActionState.Charging:
                _lastChargeProgress = _player.ChargeProgress;
                _animator.SetFloat(ActionProgressId, _lastChargeProgress);
                PlayMotion("Charge", 0f);
                break;
            case PlayerActionState.Guarding:
                PlayMotion("GuardHold", 0f);
                break;
            case PlayerActionState.NormalAttack:
                BeginAttack("Normal" + CurrentNormalStrike, PlayerAttackKind.Normal, _player.CurrentActionId);
                break;
            case PlayerActionState.IaidoSlash:
                BeginAttack("IaidoDoubleSlash", PlayerAttackKind.IaidoFinisher, _player.CurrentActionId);
                break;
            case PlayerActionState.IaidoDash:
                PlayMotion("SheathedHold", 0f);
                break;
            case PlayerActionState.Dead:
                PlayMotion("Dead", 0f, 1f);
                break;
            default:
                PlayMotion("Rest", 0f);
                break;
        }
    }

    /// <summary>name의 클립으로 전환하고 normalizedStart부터 시각 시간을 기록한다. 포즈 보간은 Animator가 담당한다.</summary>
    private void PlayMotion(string name, float blendTime, float normalizedStart = 0f)
    {
        _motion = name;
        _motionDuration = ClipLength(name);
        _motionElapsed = Mathf.Clamp01(normalizedStart) * _motionDuration;
        _animator.speed = 1f;
        int state = Animator.StringToHash("Base Layer." + name);
        if (blendTime <= 0f)
            _animator.Play(state, 0, normalizedStart);
        else
            _animator.CrossFadeInFixedTime(state, blendTime, 0, _motionElapsed);
    }

    /// <summary>kind의 실제 동작 길이와 actionId를 저장해 공격 클립의 재생 위치를 기존 드라이버에 맞춘다.</summary>
    private void BeginAttack(string clipName, PlayerAttackKind kind, int actionId)
    {
        _visualActionId = actionId;
        _attackDuration = ClipLength(clipName);
        _impactProgress = kind == PlayerAttackKind.Normal ? _normalImpactProgress : _iaidoImpactProgress;
        _impactTime = _attackDuration * _impactProgress;
        if (_actionDriver != null)
            _actionDriver.GetPresentationTiming(kind, out _impactTime, out _attackDuration);
        _guardFlash = 0f;
        _animator.SetFloat(ActionProgressId, 0f);
        PlayMotion(clipName, _actionBlendTime);
    }

    /// <summary>실제 elapsed를 클립의 적중 위치 전후로 나누어 정규화한다. 드라이버 판정 시간 변경 시 베기 지점을 함께 맞춘다.</summary>
    private float AttackProgress(float elapsed)
    {
        if (elapsed < _impactTime)
            return Mathf.Lerp(0f, _impactProgress, elapsed / Mathf.Max(0.001f, _impactTime));
        return Mathf.Lerp(_impactProgress, 1f,
            (elapsed - _impactTime) / Mathf.Max(0.001f, _attackDuration - _impactTime));
    }

    /// <summary>현재 시각 클립이 일반 또는 발도 공격인지 반환한다.</summary>
    private bool IsAttackMotion() => _motion == "Normal1" || _motion == "Normal2"
        || _motion == "Normal3" || _motion == "IaidoDoubleSlash";

    /// <summary>name에 연결된 클립 길이를 초로 반환한다. 잘못된 설정은 한 번 경고하고 최소 길이를 사용한다.</summary>
    private float ClipLength(string name)
    {
        if (_clipLengths.TryGetValue(name, out float length))
            return Mathf.Max(0.001f, length);
        Debug.LogWarning("Missing weapon animation clip: " + name, this);
        _clipLengths[name] = 0.1f;
        return 0.1f;
    }

    /// <summary>기본 자세로 부드럽게 복귀하며 가드 성공 색상을 초기화한다.</summary>
    private void MoveToRest()
    {
        _guardFlash = 0f;
        PlayMotion("Rest", _poseTransitionTime);
    }

    /// <summary>표현 렌더러만 표시하거나 숨긴다. Animator와 전투 상태는 계속 유지한다.</summary>
    private void ApplyVisibility()
    {
        if (_visible == _showWeapon)
            return;
        _visible = _showWeapon;
        foreach (Renderer renderer in _renderers)
            renderer.enabled = _visible;
    }

    /// <summary>가드 성공 및 차징 완료 색상을 인스턴스 속성으로 적용한다. 공유 재질 에셋은 변경하지 않는다.</summary>
    private void ApplyBladeTint()
    {
        float strength = Mathf.Max(_guardFlash, _player.IsChargingReady ? _readyTintStrength : 0f);
        _bladeRenderer.GetPropertyBlock(_bladeProperties, 0);
        _bladeProperties.SetColor(BaseColorId, Color.Lerp(BladeColor, ChargedBladeColor, strength));
        _bladeRenderer.SetPropertyBlock(_bladeProperties, 0);
    }
}
