using System.Collections;
using UnityEngine;

/// <summary>
/// Play-mode-only katana blockout for PlayerDebugSandbox.
/// It listens to player presentation events and never participates in combat or collision queries.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponDebugVisual : MonoBehaviour
{
    [Header("Test-only visual")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private Camera _viewCamera;
    [SerializeField] private bool _showWeapon = true;
    [SerializeField, Min(0.1f)] private float _weaponScale = 0.72f;
    [SerializeField] private Vector3 _restLocalPosition = new Vector3(0.38f, -0.30f, 0.80f);
    [SerializeField] private Vector3 _restLocalEulerAngles = new Vector3(0f, 0f, 22f);
    [SerializeField, Min(0f)] private float _poseTransitionTime = 0.12f;

    [SerializeField] private Vector3 _guardLocalPosition = new Vector3(0.10f, -0.04f, 0.72f);
    [SerializeField] private Vector3 _guardLocalEulerAngles = new Vector3(0f, 0f, 68f);
    [SerializeField, Min(0f)] private float _guardTransitionTime = 0.09f;

    private static readonly Color BladeColor = new Color(0.72f, 0.79f, 0.88f);
    private static readonly Color ChargedBladeColor = new Color(0.28f, 0.82f, 1f);

    private Transform _weaponRoot;
    private Renderer _bladeRenderer;
    private Coroutine _motionRoutine;
    
    public float GuardTransitionTime => _guardTransitionTime;

    /// <summary>테스트 씬에서 GuardStarted 이벤트가 지정 포즈까지 전달됐는지 확인합니다.</summary>
    public bool IsAtGuardPose =>
        _weaponRoot != null
        && Vector3.Distance(_weaponRoot.localPosition, _guardLocalPosition) <= 0.01f
        && Quaternion.Angle(
            _weaponRoot.localRotation,
            _restLocalRotation * Quaternion.Euler(_guardLocalEulerAngles)) <= 1f;

    /// <summary>테스트 씬에서 칼이 기본 대기 포즈와 색상으로 돌아왔는지 확인합니다.</summary>
    public bool IsAtRestPose =>
        _weaponRoot != null
        && Vector3.Distance(_weaponRoot.localPosition, _restLocalPosition) <= 0.01f
        && Quaternion.Angle(_weaponRoot.localRotation, _restLocalRotation) <= 1f
        && IsBladeAtRestColor;

    /// <summary>가드 성공 이벤트가 칼날 색상 피드백까지 전달됐는지 확인합니다.</summary>
    public bool HasGuardSuccessColor =>
        _bladeRenderer != null && ColorsMatch(_bladeRenderer.material.color, ChargedBladeColor);

    private bool IsBladeAtRestColor =>
        _bladeRenderer != null && ColorsMatch(_bladeRenderer.material.color, BladeColor);

    private static bool ColorsMatch(Color left, Color right) =>
        Mathf.Abs(left.r - right.r) <= 0.01f
        && Mathf.Abs(left.g - right.g) <= 0.01f
        && Mathf.Abs(left.b - right.b) <= 0.01f;
private Quaternion _restLocalRotation;

    private void Awake()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();

        if (_viewCamera == null)
            _viewCamera = GetComponentInChildren<Camera>(true);

        if (!_showWeapon)
            return;

        if (_viewCamera == null)
        {
            Debug.LogWarning("PlayerWeaponDebugVisual needs a child Camera to create its test weapon.", this);
            enabled = false;
            return;
        }

        BuildWeaponBlockout();
    }

    private void OnEnable()
    {
        if (_player == null)
            _player = GetComponent<PlayerController>();

        if (_player != null)
            _player.ActionEvent += OnPlayerAction;
    }

    private void OnDisable()
    {
        if (_player != null)
            _player.ActionEvent -= OnPlayerAction;

        StopMotion();
    }

    private void BuildWeaponBlockout()
    {
        GameObject rootObject = new GameObject("Debug Katana (Runtime)");
        _weaponRoot = rootObject.transform;
        _weaponRoot.SetParent(_viewCamera.transform, false);
        _weaponRoot.localPosition = _restLocalPosition;
        _restLocalRotation = Quaternion.Euler(_restLocalEulerAngles);
        _weaponRoot.localRotation = _restLocalRotation;
        _weaponRoot.localScale = Vector3.one * _weaponScale;
        _weaponRoot.gameObject.layer = _viewCamera.gameObject.layer;

        _bladeRenderer = CreatePart(
            "Blade",
            PrimitiveType.Cube,
            new Vector3(0f, 0.34f, 0f),
            new Vector3(0.055f, 0.72f, 0.028f),
            Quaternion.identity,
            BladeColor).GetComponent<Renderer>();

        CreatePart(
            "Crossguard",
            PrimitiveType.Cube,
            new Vector3(0f, -0.035f, 0f),
            new Vector3(0.25f, 0.045f, 0.075f),
            Quaternion.identity,
            new Color(0.66f, 0.45f, 0.16f));

        CreatePart(
            "Handle",
            PrimitiveType.Cylinder,
            new Vector3(0f, -0.20f, 0f),
            new Vector3(0.045f, 0.15f, 0.045f),
            Quaternion.identity,
            new Color(0.16f, 0.09f, 0.06f));

        CreatePart(
            "Pommel",
            PrimitiveType.Sphere,
            new Vector3(0f, -0.38f, 0f),
            new Vector3(0.075f, 0.075f, 0.075f),
            Quaternion.identity,
            new Color(0.66f, 0.45f, 0.16f));
    }

    private GameObject CreatePart(
        string partName,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(_weaponRoot, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        part.layer = _viewCamera.gameObject.layer;

        Collider primitiveCollider = part.GetComponent<Collider>();
        if (primitiveCollider != null)
            primitiveCollider.enabled = false;

        Renderer partRenderer = part.GetComponent<Renderer>();
        if (partRenderer != null)
            partRenderer.material.color = color;

        return part;
    }

    private void OnPlayerAction(PlayerActionEvent actionEvent)
    {
        if (_weaponRoot == null)
            return;

        switch (actionEvent.Stage)
        {
            case PlayerActionStage.GuardStarted:
                SetGuardPose();
                break;

            case PlayerActionStage.GuardSucceeded:
                SetBladeColor(ChargedBladeColor);
                break;

            case PlayerActionStage.GuardEnded:
                SetBladeColor(BladeColor);
                MoveToRest();
                break;

            case PlayerActionStage.ChargeStarted:
                SetBladeColor(BladeColor);
                MoveToPose(
                    _restLocalPosition + new Vector3(0.02f, -0.08f, -0.04f),
                    _restLocalRotation * Quaternion.Euler(0f, 0f, -10f),
                    _poseTransitionTime);
                break;

            case PlayerActionStage.ChargeReady:
                SetBladeColor(ChargedBladeColor);
                MoveToPose(
                    _restLocalPosition + new Vector3(0.02f, 0.02f, 0.02f),
                    _restLocalRotation * Quaternion.Euler(0f, 0f, -24f),
                    _poseTransitionTime);
                break;

            case PlayerActionStage.ChargeCancelled:
            case PlayerActionStage.ChargeEnded:
                SetBladeColor(BladeColor);
                MoveToRest();
                break;

            case PlayerActionStage.AttackStarted:
                if (actionEvent.State == PlayerActionState.NormalAttack)
                    PlaySlash(false);
                break;

            case PlayerActionStage.SlashStarted:
                PlaySlash(true);
                break;

            case PlayerActionStage.RecoveryEnded:
                SetBladeColor(BladeColor);
                MoveToRest();
                break;

            case PlayerActionStage.StateChanged:
                ApplyPoseForState(actionEvent.State);
                break;
        }
    }

    private void ApplyPoseForState(PlayerActionState state)
    {
        switch (state)
        {
            case PlayerActionState.Free:
            case PlayerActionState.Recovery:
            case PlayerActionState.Paused:
            case PlayerActionState.Dead:
                SetBladeColor(BladeColor);
                MoveToRest();
                break;

            case PlayerActionState.Guarding:
                SetGuardPose();
                break;

            case PlayerActionState.Charging:
                if (_player != null && _player.IsChargingReady)
                {
                    SetBladeColor(ChargedBladeColor);
                    MoveToPose(
                        _restLocalPosition + new Vector3(0.02f, 0.02f, 0.02f),
                        _restLocalRotation * Quaternion.Euler(0f, 0f, -24f),
                        _poseTransitionTime);
                }
                else
                {
                    SetBladeColor(BladeColor);
                    MoveToPose(
                        _restLocalPosition + new Vector3(0.02f, -0.08f, -0.04f),
                        _restLocalRotation * Quaternion.Euler(0f, 0f, -10f),
                        _poseTransitionTime);
                }
                break;
        }
    }

    private void SetGuardPose()
    {
        SetBladeColor(BladeColor);
        MoveToPose(
            _guardLocalPosition,
            _restLocalRotation * Quaternion.Euler(_guardLocalEulerAngles),
            _guardTransitionTime);
    }


    private void PlaySlash(bool iaido)
    {
        StopMotion();
        SetBladeColor(BladeColor);
        _motionRoutine = StartCoroutine(SlashRoutine(iaido));
    }

    private IEnumerator SlashRoutine(bool iaido)
    {
        Vector3 windupOffset = iaido
            ? new Vector3(0.14f, -0.07f, -0.08f)
            : new Vector3(0.10f, -0.035f, -0.06f);
        Vector3 impactOffset = iaido
            ? new Vector3(-0.25f, 0.10f, 0.16f)
            : new Vector3(-0.18f, 0.06f, 0.10f);

        Quaternion windupRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, -62f);
        Quaternion impactRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, 62f);

        yield return AnimateTo(
            _restLocalPosition + windupOffset,
            windupRotation,
            iaido ? 0.05f : 0.04f);

        yield return AnimateTo(
            _restLocalPosition + impactOffset,
            impactRotation,
            iaido ? 0.13f : 0.11f);

        yield return AnimateTo(_restLocalPosition, _restLocalRotation, iaido ? 0.23f : 0.18f);
        _motionRoutine = null;
    }

    private void MoveToPose(Vector3 localPosition, Quaternion localRotation, float duration)
    {
        StopMotion();
        _motionRoutine = StartCoroutine(AnimateTo(localPosition, localRotation, duration));
    }

    private void MoveToRest()
    {
        MoveToPose(_restLocalPosition, _restLocalRotation, _poseTransitionTime);
    }

    private IEnumerator AnimateTo(Vector3 localPosition, Quaternion localRotation, float duration)
    {
        if (_weaponRoot == null)
            yield break;

        Vector3 startPosition = _weaponRoot.localPosition;
        Quaternion startRotation = _weaponRoot.localRotation;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            _weaponRoot.localPosition = localPosition;
            _weaponRoot.localRotation = localRotation;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _weaponRoot.localPosition = Vector3.Lerp(startPosition, localPosition, t);
            _weaponRoot.localRotation = Quaternion.Slerp(startRotation, localRotation, t);
            yield return null;
        }

        _weaponRoot.localPosition = localPosition;
        _weaponRoot.localRotation = localRotation;
    }

    private void SetBladeColor(Color color)
    {
        if (_bladeRenderer != null)
            _bladeRenderer.material.color = color;
    }

    private void StopMotion()
    {
        if (_motionRoutine == null)
            return;

        StopCoroutine(_motionRoutine);
        _motionRoutine = null;
    }
}
