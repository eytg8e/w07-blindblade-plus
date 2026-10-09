using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test-only scenario shortcuts for PlayerDebugSandbox.
/// It repositions the existing test target and wall without changing combat rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSandboxScenarioControls : MonoBehaviour
{
    [Header("Sandbox Controls")] [SerializeField]
    private bool _showControls = true;

    [SerializeField, Min(0.5f)] private float _normalHitDistance = 1f;
    [SerializeField, Min(1f)] private float _iaidoHitDistance = 3f;
    [SerializeField, Min(0.5f)] private float _wallDistance = 1f;
    [SerializeField, Min(1f)] private float _wallTargetDistance = 4f;

    [Header("Movement Measurements")] [SerializeField, Min(0.05f)]
    private float _movementWarmupDuration = 0.35f;

    [SerializeField, Min(0.1f)] private float _movementSampleDuration = 0.5f;
    [SerializeField, Min(0.05f)] private float _movementBrakeSampleDuration = 0.25f;
    [SerializeField, Range(0.01f, 0.5f)] private float _movementDiagonalTolerance = 0.15f;
    [SerializeField, Min(0.5f)] private float _movementTestWallDistance = 1.5f;

    private PlayerController _player;
    private Camera _viewCamera;
    private PlayerTestTarget _testTarget;
    private GameObject _wallObject;
    private Collider _wallCollider;
    private string _scenarioLabel = "Starting test setup";
    private Coroutine _validationRoutine;
    private PlayerAttackOutcome _observedOutcome;
    private float _observedRecoveryDuration;
    private float _observedRecoveryStartedAt;
    private float _observedRecoveryElapsed;
    private bool _hasObservedRecoveryStart;
    private bool _hasObservedRecoveryEnd;
    private bool _lastValidationCasePassed;

    private bool _observedGuardStarted;
    private bool _observedGuardSucceeded;
    private bool _observedGuardEnded;
    private int _validationPassCount;

    private enum ValidationCase
    {
        NormalHit,
        NormalMiss,
        IaidoHit,
        IaidoMiss,
        IaidoWallBlocked
    }

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _viewCamera = GetComponentInChildren<Camera>(true);

        GameObject targetObject = GameObject.Find("Test Enemy");
        if (targetObject != null)
            _testTarget = targetObject.GetComponent<PlayerTestTarget>();

        _wallObject = GameObject.Find("Test Wall");
        if (_wallObject != null)
            _wallCollider = _wallObject.GetComponent<Collider>();

        if (_player == null || _viewCamera == null ||
            _testTarget == null ||
            _wallObject == null || _wallCollider == null)
        {
            Debug.LogError(
                "PlayerSandboxScenarioControls requires PlayerController, a child Camera, Test Enemy, and Test Wall.",
                this);
            enabled = false;
            return;
        }

        SetTargetCenter(_normalHitDistance);
        HideWall();
        _scenarioLabel = "Ready: target centered, wall hidden";
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || _validationRoutine != null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
            PrepareNormalHit();
        else if (keyboard.f2Key.wasPressedThisFrame)
            PrepareNormalMiss();
        else if (keyboard.f3Key.wasPressedThisFrame)
            PrepareIaidoHit();
        else if (keyboard.f4Key.wasPressedThisFrame)
            PrepareIaidoMiss();
        else if (keyboard.f5Key.wasPressedThisFrame)
            PrepareIaidoWallBlock();
        else if (keyboard.f6Key.wasPressedThisFrame)
            ResetSetup();
        else if (keyboard.f7Key.wasPressedThisFrame)
            RunRecoveryValidation();
        else if (keyboard.f8Key.wasPressedThisFrame)
            RunGuardValidation();
        else if (keyboard.f9Key.wasPressedThisFrame)
            RunMovementTuning();
    }

    private void OnGUI()
    {
        if (!_showControls)
            return;

        const float width = 405f;
        const float height = 252f;
        float x = Mathf.Max(12f, Screen.width - width - 12f);
        Rect panel = new Rect(x, 12f, width, height);
        GUI.Box(panel, "PLAYER TEST SCENARIOS");
        GUI.Label(
            new Rect(panel.x + 12f, panel.y + 28f, width - 24f, height - 36f),
            "F1  Normal attack hit setup\n"
            + "F2  Normal attack miss setup\n"
            + "F3  Iaido hit setup\n"
            + "F4  Iaido miss setup\n"
            + "F5  Iaido wall block setup\n"
            + "F6  Reset setup\n"
            + "F7  Run all 8 recovery checks\n"
            + "F8  Guard pose and block check\n"
            + "F9  Measure movement tuning\n"
            + _scenarioLabel
            + "\nLeft click: attack | Hold Left Ctrl: charge | Right click: guard | F: probe");
    }

    private void PrepareNormalHit()
    {
        SetTargetCenter(_normalHitDistance);
        HideWall();
        SetScenario("Normal hit: target centered and in range");
    }

    private void PrepareNormalMiss()
    {
        Vector3 behindPlayer = _viewCamera.transform.position - GetFlatForward() * 2f;
        _testTarget.ResetForTest(behindPlayer);
        HideWall();
        SetScenario("Normal miss: target behind player");
    }

    private void PrepareIaidoHit()
    {
        SetTargetCenter(_iaidoHitDistance);
        HideWall();
        SetScenario("Iaido hit: target centered ahead");
    }

    private void PrepareIaidoMiss()
    {
        Vector3 behindPlayer = _viewCamera.transform.position - GetFlatForward() * 2f;
        _testTarget.ResetForTest(behindPlayer);
        HideWall();
        SetScenario("Iaido miss: no target ahead");
    }

    private void PrepareIaidoWallBlock()
    {
        SetTargetCenter(_wallTargetDistance);

        _wallObject.SetActive(true);
        _wallCollider.enabled = true;
        _wallObject.transform.position =
            _viewCamera.transform.position + GetFlatForward() * _wallDistance;
        SetScenario("Iaido wall block: non-destructible wall ahead");
    }

    private void ResetSetup()
    {
        SetTargetCenter(_normalHitDistance);
        HideWall();
        SetScenario("Reset: target centered, wall hidden");
    }

    private void SetTargetCenter(float distance)
    {
        Vector3 targetCenter = _viewCamera.transform.position + GetFlatForward() * distance;
        _testTarget.ResetForTest(targetCenter);
    }

    private Vector3 GetFlatForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(_viewCamera.transform.forward, Vector3.up).normalized;
        return forward.sqrMagnitude > 0.0001f ? forward : transform.forward;
    }

    private void HideWall()
    {
        _wallCollider.enabled = false;
        _wallObject.SetActive(false);
    }

    private void SetScenario(string label)
    {
        _scenarioLabel = label;
        Debug.Log("[PlayerSandboxScenario] " + label, this);
    }

    private void FinishRecoveryValidation()
    {
        _scenarioLabel = $"Recovery checks: {_validationPassCount}/8 passed";
        Debug.Log($"[RecoveryCheck] Sequence finished: {_validationPassCount}/8 passed.", this);
        _validationRoutine = null;
    }


    private void RecordValidationFailure(string label, string reason)
    {
        _lastValidationCasePassed = false;
        _scenarioLabel = $"FAIL: {label} ({reason})";
        Debug.LogError($"[RecoveryCheck] FAIL {label}: {reason}", this);
    }


    private void ResetRecoveryObservation()
    {
        _observedOutcome = PlayerAttackOutcome.None;
        _observedRecoveryDuration = 0f;
        _observedRecoveryStartedAt = 0f;
        _observedRecoveryElapsed = 0f;
        _hasObservedRecoveryStart = false;
        _hasObservedRecoveryEnd = false;
        _lastValidationCasePassed = false;
    }


    private IEnumerator ValidateCase(ValidationCase testCase)
    {
        string label;
        PlayerAttackOutcome expectedOutcome;
        float expectedRecovery;
        bool isIaido;

        switch (testCase)
        {
            case ValidationCase.NormalHit:
                label = "Normal hit";
                expectedOutcome = PlayerAttackOutcome.Hit;
                expectedRecovery = 0.25f;
                isIaido = false;
                PrepareNormalHit();
                break;
            case ValidationCase.NormalMiss:
                label = "Normal miss";
                expectedOutcome = PlayerAttackOutcome.Miss;
                expectedRecovery = 0.25f;
                isIaido = false;
                PrepareNormalMiss();
                break;
            case ValidationCase.IaidoHit:
                label = "Iaido hit";
                expectedOutcome = PlayerAttackOutcome.Hit;
                expectedRecovery = 0.15f;
                isIaido = true;
                PrepareIaidoHit();
                break;
            case ValidationCase.IaidoMiss:
                label = "Iaido miss";
                expectedOutcome = PlayerAttackOutcome.Miss;
                expectedRecovery = 0.7f;
                isIaido = true;
                PrepareIaidoMiss();
                break;
            default:
                label = "Iaido wall blocked";
                expectedOutcome = PlayerAttackOutcome.WallBlocked;
                expectedRecovery = 0.9f;
                isIaido = true;
                PrepareIaidoWallBlock();
                break;
        }

        _scenarioLabel = $"Checking {label}...";
        ResetRecoveryObservation();

        if (isIaido)
        {
            if (!_player.BeginCharge())
            {
                RecordValidationFailure(label, "could not start charge");
                yield break;
            }

            float chargeDeadline = Time.time + 2f;
            while (!_player.IsChargingReady && Time.time < chargeDeadline)
                yield return null;

            if (!_player.IsChargingReady)
            {
                RecordValidationFailure(label, "charge did not become ready within 2 seconds");
                yield break;
            }

            if (!_player.RequestIaidoAttack())
            {
                RecordValidationFailure(label, "iaido attack request was rejected");
                yield break;
            }
        }
        else if (!_player.RequestNormalAttack())
        {
            RecordValidationFailure(label, "normal attack request was rejected");
            yield break;
        }

        float actionDeadline = Time.time + (isIaido ? 4f : 2f);
        while (!_hasObservedRecoveryStart && Time.time < actionDeadline)
            yield return null;

        if (!_hasObservedRecoveryStart)
        {
            RecordValidationFailure(label, "RecoveryStarted event was not received");
            yield break;
        }

        float recoveryDeadline = Time.time + expectedRecovery + 1f;
        while (!_hasObservedRecoveryEnd && Time.time < recoveryDeadline)
            yield return null;

        if (!_hasObservedRecoveryEnd)
        {
            RecordValidationFailure(label, "RecoveryEnded event was not received");
            yield break;
        }

        float tolerance = Mathf.Max(0.08f, Time.deltaTime * 3f);
        bool outcomeMatches = _observedOutcome == expectedOutcome;
        bool configuredTimeMatches = Mathf.Abs(_observedRecoveryDuration - expectedRecovery) <= 0.001f;
        bool elapsedTimeMatches = Mathf.Abs(_observedRecoveryElapsed - expectedRecovery) <= tolerance;
        _lastValidationCasePassed = outcomeMatches && configuredTimeMatches && elapsedTimeMatches;

        string result = _lastValidationCasePassed ? "PASS" : "FAIL";
        _scenarioLabel = $"{result}: {label}";
        Debug.Log(
            $"[RecoveryCheck] {result} {label}: outcome={_observedOutcome}/{expectedOutcome}, "
            + $"configured={_observedRecoveryDuration:0.000}/{expectedRecovery:0.000}s, "
            + $"elapsed={_observedRecoveryElapsed:0.000}s (tolerance {tolerance:0.000}s)",
            this);

        if (_lastValidationCasePassed)
            _validationPassCount++;
    }

    private IEnumerator ValidateRecoveryInputLock()
    {
        const string label = "Recovery input lock";
        PrepareNormalHit();
        ResetRecoveryObservation();

        if (!_player.RequestNormalAttack())
        {
            RecordValidationFailure(label, "could not start the setup attack");
            yield break;
        }

        float startDeadline = Time.time + 2f;
        while (!_hasObservedRecoveryStart && Time.time < startDeadline)
            yield return null;

        if (!_hasObservedRecoveryStart)
        {
            RecordValidationFailure(label, "RecoveryStarted event was not received");
            yield break;
        }

        Vector3 positionBeforeRequests = _player.transform.position;
        PlayerMotor motor = _player.GetComponent<PlayerMotor>();

        _player.SetMovementInput(Vector2.up);
        bool normalAttackRejected = !_player.RequestNormalAttack();
        bool guardRejected = !_player.BeginGuard();
        bool chargeRejected = !_player.BeginCharge();
        bool iaidoRejected = !_player.RequestIaidoAttack();
        bool remainedInRecovery = _player.CurrentState == PlayerActionState.Recovery;

        yield return new WaitForSecondsRealtime(0.1f);

        float horizontalDisplacement = Vector3.ProjectOnPlane(
            _player.transform.position - positionBeforeRequests,
            Vector3.up).magnitude;
        bool movementStayedLocked = horizontalDisplacement <= 0.01f
                                    && (motor == null || motor.HorizontalSpeed <= 0.05f);

        float endDeadline = Time.time + 1f;
        while (!_hasObservedRecoveryEnd && Time.time < endDeadline)
            yield return null;

        if (!_hasObservedRecoveryEnd)
        {
            RecordValidationFailure(label, "RecoveryEnded event was not received");
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.1f);

        bool returnedToFree = _player.CurrentState == PlayerActionState.Free;
        bool noActionWasQueued = returnedToFree && !_player.IsGuarding && !_player.IsCharging;
        _lastValidationCasePassed =
            normalAttackRejected
            && guardRejected
            && chargeRejected
            && iaidoRejected
            && remainedInRecovery
            && movementStayedLocked
            && noActionWasQueued;

        string result = _lastValidationCasePassed ? "PASS" : "FAIL";
        _scenarioLabel = $"{result}: {label}";
        Debug.Log(
            $"[RecoveryCheck] {result} {label}: attackRejected={normalAttackRejected}, "
            + $"guardRejected={guardRejected}, chargeRejected={chargeRejected}, "
            + $"iaidoRejected={iaidoRejected}, movementLocked={movementStayedLocked}, "
            + $"queuedAfterRecovery={!noActionWasQueued}.",
            this);

        if (_lastValidationCasePassed)
            _validationPassCount++;
    }


    private IEnumerator RecoveryValidationRoutine()
    {
        yield return ValidateCase(ValidationCase.NormalHit);
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateCase(ValidationCase.NormalMiss);
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateCase(ValidationCase.IaidoHit);
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateCase(ValidationCase.IaidoMiss);
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateCase(ValidationCase.IaidoWallBlocked);
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateRecoveryInputLock();
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidatePauseDuringRecovery();
        if (!_lastValidationCasePassed)
        {
            FinishRecoveryValidation();
            yield break;
        }

        yield return ValidateDeathDuringRecovery();
        FinishRecoveryValidation();
    }

    private IEnumerator ValidateDeathDuringRecovery()
    {
        const string label = "Death during recovery";
        PrepareNormalHit();
        ResetRecoveryObservation();

        if (!_player.RequestNormalAttack())
        {
            RecordValidationFailure(label, "normal attack request was rejected");
            yield break;
        }

        float startDeadline = Time.time + 2f;
        while (!_hasObservedRecoveryStart && Time.time < startDeadline)
            yield return null;

        if (!_hasObservedRecoveryStart)
        {
            RecordValidationFailure(label, "RecoveryStarted event was not received");
            yield break;
        }

        _player.SetDead();
        bool enteredDeadState = _player.CurrentState == PlayerActionState.Dead;
        bool recoveryCleared = _player.RecoveryRemaining <= 0.001f;
        yield return new WaitForSecondsRealtime(0.35f);

        bool didNotEndRecoveryAfterDeath = !_hasObservedRecoveryEnd;
        _lastValidationCasePassed =
            enteredDeadState && recoveryCleared && didNotEndRecoveryAfterDeath;

        string result = _lastValidationCasePassed ? "PASS" : "FAIL";
        _scenarioLabel = $"{result}: {label}";
        Debug.Log(
            $"[RecoveryCheck] {result} {label}: dead={enteredDeadState}, "
            + $"remainingCleared={recoveryCleared}, recoveryEndedEvent={_hasObservedRecoveryEnd}.",
            this);

        if (_lastValidationCasePassed)
            _validationPassCount++;
    }


    private IEnumerator ValidatePauseDuringRecovery()
    {
        const string label = "Pause during recovery";
        PrepareNormalHit();
        ResetRecoveryObservation();

        if (!_player.RequestNormalAttack())
        {
            RecordValidationFailure(label, "normal attack request was rejected");
            yield break;
        }

        float startDeadline = Time.time + 2f;
        while (!_hasObservedRecoveryStart && Time.time < startDeadline)
            yield return null;

        if (!_hasObservedRecoveryStart)
        {
            RecordValidationFailure(label, "RecoveryStarted event was not received");
            yield break;
        }

        float remainingBeforePause = _player.RecoveryRemaining;
        _player.SetPaused(true);
        yield return new WaitForSecondsRealtime(0.25f);

        bool stayedPaused = _player.CurrentState == PlayerActionState.Paused;
        bool timerFrozen = Mathf.Abs(_player.RecoveryRemaining - remainingBeforePause) <= 0.001f;
        bool didNotEndWhilePaused = !_hasObservedRecoveryEnd;
        _player.SetPaused(false);

        if (!stayedPaused || !timerFrozen || !didNotEndWhilePaused)
        {
            RecordValidationFailure(
                label,
                $"paused={stayedPaused}, timerFrozen={timerFrozen}, endedWhilePaused={!didNotEndWhilePaused}");
            yield break;
        }

        float resumeDeadline = Time.time + 1f;
        while (!_hasObservedRecoveryEnd && Time.time < resumeDeadline)
            yield return null;

        _lastValidationCasePassed =
            _hasObservedRecoveryEnd && _player.CurrentState == PlayerActionState.Free;
        string result = _lastValidationCasePassed ? "PASS" : "FAIL";
        _scenarioLabel = $"{result}: {label}";
        Debug.Log(
            $"[RecoveryCheck] {result} {label}: remaining frozen at {remainingBeforePause:0.000}s, "
            + $"then resumed and ended={_hasObservedRecoveryEnd}.",
            this);

        if (_lastValidationCasePassed)
            _validationPassCount++;
    }

    private IEnumerator GuardValidationRoutine()
    {
        const string label = "Guard feedback";
        PlayerWeaponDebugVisual visual = GetComponent<PlayerWeaponDebugVisual>();
        if (visual == null)
        {
            Debug.LogError("[GuardCheck] FAIL: PlayerWeaponDebugVisual is missing.", this);
            _scenarioLabel = "FAIL: Guard visual missing";
            _validationRoutine = null;
            yield break;
        }

        _observedGuardStarted = false;
        _observedGuardSucceeded = false;
        _observedGuardEnded = false;

        bool guardAccepted = _player.BeginGuard();
        if (!guardAccepted)
        {
            Debug.LogError("[GuardCheck] FAIL: guard start request was rejected.", this);
            _scenarioLabel = "FAIL: Guard start rejected";
            _validationRoutine = null;
            yield break;
        }

        float poseDeadline = Time.unscaledTime + Mathf.Max(1.5f, visual.GuardTransitionTime + 0.5f);
        while ((!_observedGuardStarted || !visual.IsAtGuardPose)
               && Time.unscaledTime < poseDeadline)
        {
            yield return null;
        }

        bool enteredGuardPose = _player.IsGuarding
                                && _observedGuardStarted
                                && visual.IsAtGuardPose;
        bool incomingAttackBlocked = _player.TryBlockIncomingAttack();
        if (incomingAttackBlocked)
            _player.NotifyGuardSuccess();

        yield return null;

        bool successEventObserved = _observedGuardSucceeded;
        bool successColorShown = visual.HasGuardSuccessColor;
        bool stayedGuardingAfterSuccess = _player.IsGuarding;

        _player.EndGuard();
        float releaseDeadline = Time.unscaledTime + visual.GuardTransitionTime + 1f;
        while ((!_observedGuardEnded || !visual.IsAtRestPose)
               && Time.unscaledTime < releaseDeadline)
        {
            yield return null;
        }

        bool returnedToRestPose = !_player.IsGuarding
                                  && _player.CurrentState == PlayerActionState.Free
                                  && _observedGuardEnded
                                  && visual.IsAtRestPose;

        bool passed = guardAccepted
                      && enteredGuardPose
                      && incomingAttackBlocked
                      && successEventObserved
                      && successColorShown
                      && stayedGuardingAfterSuccess
                      && returnedToRestPose;

        _scenarioLabel = passed ? "PASS: Guard pose / block / release" : "FAIL: Guard feedback";
        Debug.Log(
            $"[GuardCheck] {(passed ? "PASS" : "FAIL")} {label}: "
            + $"started={_observedGuardStarted}, guardPose={enteredGuardPose}, "
            + $"blockQuery={incomingAttackBlocked}, successEvent={successEventObserved}, "
            + $"successColor={successColorShown}, endEvent={_observedGuardEnded}, "
            + $"restPose={returnedToRestPose}.",
            this);
        _validationRoutine = null;
    }

    private IEnumerator MovementTuningRoutine()
    {
        const string label = "Movement tuning";
        PlayerMotor motor = GetComponent<PlayerMotor>();
        CharacterController characterController = GetComponent<CharacterController>();
        PlayerInputCommandAdapter inputAdapter = GetComponent<PlayerInputCommandAdapter>();

        if (motor == null || characterController == null || _testTarget == null
            || _wallObject == null || _wallCollider == null)
        {
            Debug.LogError("[MovementTune] FAIL: motor, CharacterController, test target, or test wall is missing.",
                this);
            _scenarioLabel = "FAIL: Movement test setup missing";
            _validationRoutine = null;
            yield break;
        }

        Vector3 startingPosition = _player.transform.position;
        Quaternion startingRotation = _player.transform.rotation;
        Vector3 flatForward = GetFlatForward();
        bool restoreInputAdapter = inputAdapter != null && inputAdapter.enabled;
        Collider[] targetColliders = _testTarget.GetComponentsInChildren<Collider>(true);
        bool[] targetColliderStates = new bool[targetColliders.Length];

        if (restoreInputAdapter)
            inputAdapter.enabled = false;

        // The sandbox target sits directly in front of the player and would invalidate the forward sample.
        for (int i = 0; i < targetColliders.Length; i++)
        {
            targetColliderStates[i] = targetColliders[i].enabled;
            targetColliders[i].enabled = false;
        }

        _player.SetMovementInput(Vector2.zero);
        HideWall();
        yield return new WaitForSecondsRealtime(0.15f);

        float settleDeadline = Time.realtimeSinceStartup + 2f;
        while (motor.HorizontalSpeed > 0.05f && Time.realtimeSinceStartup < settleDeadline)
            yield return null;
        bool accelerationStartedFromRest = motor.HorizontalSpeed <= 0.05f;
        RestorePlayerTransform(characterController, startingPosition, startingRotation);

        // Measure game time from rest until the motor reaches 90% of its configured walk speed.
        float targetSpeed = motor.WalkSpeed * 0.9f;
        float accelerationStartTime = Time.time;
        float accelerationDeadline = accelerationStartTime + 2f;
        _player.SetMovementInput(Vector2.up);
        while (targetSpeed > 0.05f
               && motor.HorizontalSpeed < targetSpeed
               && Time.time < accelerationDeadline)
        {
            yield return null;
        }

        float timeToNinetyPercent = Mathf.Max(0f, Time.time - accelerationStartTime);
        bool accelerationReachedTarget = targetSpeed <= 0.05f
                                         || motor.HorizontalSpeed >= targetSpeed;

        _player.SetMovementInput(Vector2.zero);
        yield return new WaitForSecondsRealtime(0.15f);
        settleDeadline = Time.realtimeSinceStartup + 2f;
        while (motor.HorizontalSpeed > 0.05f && Time.realtimeSinceStartup < settleDeadline)
            yield return null;
        bool accelerationTestStopped = motor.HorizontalSpeed <= 0.05f;
        RestorePlayerTransform(characterController, startingPosition, startingRotation);

        // Warm-up lets acceleration reach its configured top speed before distance sampling.
        _player.SetMovementInput(Vector2.up);
        yield return new WaitForSeconds(_movementWarmupDuration);
        Vector3 forwardSampleStart = _player.transform.position;
        yield return new WaitForSeconds(_movementSampleDuration);
        Vector3 forwardSampleEnd = _player.transform.position;
        float forwardDistance = Vector3.ProjectOnPlane(
            forwardSampleEnd - forwardSampleStart,
            Vector3.up).magnitude;

        _player.SetMovementInput(Vector2.zero);
        yield return new WaitForSecondsRealtime(_movementBrakeSampleDuration);
        RestorePlayerTransform(characterController, startingPosition, startingRotation);

        _player.SetMovementInput(Vector2.one);
        yield return new WaitForSeconds(_movementWarmupDuration);
        Vector3 diagonalSampleStart = _player.transform.position;
        yield return new WaitForSeconds(_movementSampleDuration);
        Vector3 diagonalSampleEnd = _player.transform.position;
        float diagonalDistance = Vector3.ProjectOnPlane(
            diagonalSampleEnd - diagonalSampleStart,
            Vector3.up).magnitude;
        float diagonalRatio = forwardDistance > 0.001f
            ? diagonalDistance / forwardDistance
            : 0f;

        _player.SetMovementInput(Vector2.zero);
        Vector3 brakeStart = _player.transform.position;
        yield return new WaitForSecondsRealtime(_movementBrakeSampleDuration);
        float brakingDistance = Vector3.ProjectOnPlane(
            _player.transform.position - brakeStart,
            Vector3.up).magnitude;
        bool cameToRest = motor.HorizontalSpeed <= 0.05f;

        RestorePlayerTransform(characterController, startingPosition, startingRotation);
        _wallObject.transform.position =
            _viewCamera.transform.position + flatForward * _movementTestWallDistance;
        _wallObject.SetActive(true);
        _wallCollider.enabled = true;

        _player.SetMovementInput(Vector2.up);
        yield return new WaitForSeconds(_movementWarmupDuration + _movementSampleDuration);
        float wallTravelDistance = Vector3.ProjectOnPlane(
            _player.transform.position - startingPosition,
            Vector3.up).magnitude;
        _player.SetMovementInput(Vector2.zero);
        HideWall();
        RestorePlayerTransform(characterController, startingPosition, startingRotation);

        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
                targetColliders[i].enabled = targetColliderStates[i];
        }

        if (restoreInputAdapter)
            inputAdapter.enabled = true;

        float walkDistanceRatioError = Mathf.Abs(diagonalRatio - 1f);
        bool diagonalSpeedNormalized = forwardDistance > 0.1f
                                       && diagonalDistance > 0.1f
                                       && walkDistanceRatioError <= _movementDiagonalTolerance;
        bool wallStoppedMovement = wallTravelDistance > 0.1f
                                   && wallTravelDistance < forwardDistance * 0.85f;
        bool passed = accelerationStartedFromRest
                      && accelerationReachedTarget
                      && accelerationTestStopped
                      && diagonalSpeedNormalized
                      && cameToRest
                      && wallStoppedMovement;

        string result = passed ? "PASS" : "REVIEW";
        _scenarioLabel =
            $"{result}: accel90={timeToNinetyPercent:0.00}s, ratio={diagonalRatio:0.00}, "
            + $"brake={brakingDistance:0.00}m, wall={wallTravelDistance:0.00}m";
        Debug.Log(
            $"[MovementTune] {result} {label}: walk={motor.WalkSpeed:0.00}m/s, "
            + $"acceleration={motor.Acceleration:0.00}, time-to-90%={timeToNinetyPercent:0.000}s "
            + $"(reached={accelerationReachedTarget}, settled={accelerationTestStopped}), "
            + $"deceleration={motor.Deceleration:0.00}, forward={forwardDistance:0.00}m, "
            + $"diagonal={diagonalDistance:0.00}m (ratio {diagonalRatio:0.00}, "
            + $"allowed error {_movementDiagonalTolerance:0.00}), "
            + $"braking={brakingDistance:0.00}m, stopped={cameToRest}, "
            + $"wall travel={wallTravelDistance:0.00}m.",
            this);
        _validationRoutine = null;
    }

    private static void RestorePlayerTransform(
        CharacterController characterController,
        Vector3 position,
        Quaternion rotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (wasEnabled)
            characterController.enabled = false;

        characterController.transform.position = position;
        characterController.transform.rotation = rotation;

        if (wasEnabled)
            characterController.enabled = true;
    }


    public void RunRecoveryValidation()
    {
        if (_validationRoutine != null)
            return;

        if (_player == null || _player.CurrentState != PlayerActionState.Free)
        {
            _scenarioLabel = "Auto check requires the player to be Free";
            Debug.LogWarning("[RecoveryCheck] Start the sequence while the player is Free.", this);
            return;
        }

        _validationPassCount = 0;
        _validationRoutine = StartCoroutine(RecoveryValidationRoutine());
    }

    public void RunGuardValidation()
    {
        if (_validationRoutine != null)
            return;

        if (_player == null || _player.CurrentState != PlayerActionState.Free)
        {
            _scenarioLabel = "Guard check requires the player to be Free";
            Debug.LogWarning("[GuardCheck] Start the sequence while the player is Free.", this);
            return;
        }

        if (Time.timeScale <= 0f)
        {
            _scenarioLabel = "Guard check requires Time.timeScale > 0";
            Debug.LogWarning("[GuardCheck] Time.timeScale must be greater than zero.", this);
            return;
        }

        _validationRoutine = StartCoroutine(GuardValidationRoutine());
    }

    public void RunMovementTuning()
    {
        if (_validationRoutine != null)
            return;

        if (_player == null || _player.CurrentState != PlayerActionState.Free)
        {
            _scenarioLabel = "Movement check requires the player to be Free";
            Debug.LogWarning("[MovementTune] Start the measurement while the player is Free.", this);
            return;
        }

        if (Time.timeScale <= 0f)
        {
            _scenarioLabel = "Movement check requires Time.timeScale > 0";
            Debug.LogWarning("[MovementTune] Time.timeScale must be greater than zero.", this);
            return;
        }

        _validationRoutine = StartCoroutine(MovementTuningRoutine());
    }


    private void HandlePlayerActionEvent(PlayerActionEvent actionEvent)
    {
        switch (actionEvent.Stage)
        {
            case PlayerActionStage.GuardStarted:
                _observedGuardStarted = true;
                break;
            case PlayerActionStage.GuardSucceeded:
                _observedGuardSucceeded = true;
                break;
            case PlayerActionStage.GuardEnded:
                _observedGuardEnded = true;
                break;
            case PlayerActionStage.HitConfirmed:
                _observedOutcome = PlayerAttackOutcome.Hit;
                break;
            case PlayerActionStage.Missed:
                _observedOutcome = PlayerAttackOutcome.Miss;
                break;
            case PlayerActionStage.DashBlocked:
                _observedOutcome = actionEvent.Outcome;
                break;
            case PlayerActionStage.RecoveryStarted:
                if (_observedOutcome == PlayerAttackOutcome.None)
                    _observedOutcome = actionEvent.Outcome;
                _observedRecoveryDuration = _player != null ? _player.RecoveryRemaining : 0f;
                _observedRecoveryStartedAt = Time.time;
                _hasObservedRecoveryStart = true;
                break;
            case PlayerActionStage.RecoveryEnded:
                if (_hasObservedRecoveryStart)
                {
                    _observedRecoveryElapsed = Mathf.Max(0f, Time.time - _observedRecoveryStartedAt);
                    _hasObservedRecoveryEnd = true;
                }

                break;
        }
    }


    private void OnDisable()
    {
        if (_player != null)
            _player.ActionEvent -= HandlePlayerActionEvent;

        if (_validationRoutine != null)
        {
            StopCoroutine(_validationRoutine);
            _validationRoutine = null;
        }
    }


    private void OnEnable()
    {
        if (_player != null)
            _player.ActionEvent += HandlePlayerActionEvent;
    }
}