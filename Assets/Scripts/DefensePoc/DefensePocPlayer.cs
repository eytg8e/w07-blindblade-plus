using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DefenseTest에서 고정 위치 시점, 집중, 극한 회피와 광역 참격을 처리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class DefensePocPlayer : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private KatanaCombatCoordinator _combat;
    [SerializeField] private Transform _bladeBase;
    [SerializeField] private ParticleSystem _normalSlash;

    [Header("수평 시점")]
    [SerializeField, Min(1f)] private float _keyboardDegreesPerSecond = 120f;
    [SerializeField, Min(0f)] private float _mouseSensitivity = 0.12f;
    [SerializeField, Min(0.01f)] private float _lookSmoothTime = 0.1f;
    [SerializeField, Min(0.01f)] private float _counterTurnDuration = 0.5f;

    [Header("집중과 극한 회피")]
    [SerializeField, Min(0.01f)] private float _focusCapacity = 2f;
    [SerializeField, Min(0.01f)] private float _focusRecoveryDuration = 3f;
    [SerializeField, Min(0.01f)] private float _detectionWindow = 0.3f;
    [SerializeField, Min(0.01f)] private float _projectileDistance = 10f;
    [SerializeField, Range(0.01f, 1f)] private float _bulletTimeScale = 0.3f;
    [SerializeField, Min(0.01f)] private float _counterGraceDuration = 2f;
    [SerializeField, Min(0.01f)] private float _invulnerabilityDuration = 26f / 60f;
    [SerializeField, Min(0.01f)] private float _focusRevealRadius = 20f;

    [Header("극한 참격")]
    [SerializeField, Min(0.01f)] private float _normalAttackRange = 1.5f;
    [SerializeField, Min(0.01f)] private float _normalHitRadius = 0.1f;
    [SerializeField, Min(1f)] private float _slashMultiplier = 5f;
    private readonly HashSet<EnemyHealth> _slashVictims = new HashSet<EnemyHealth>();
    private float _focusRemaining;
    private float _targetYaw;
    private float _lookVelocity;
    private float _detectionEndsAt;
    private float _counterGraceEndsAt;
    private float _invulnerableUntil;
    private float _turnStartedAt;
    private float _turnFromYaw;
    private float _timeScaleBeforeBullet = 1f;
    private bool _shiftHeld;
    private bool _wasShiftHeld;
    private bool _bulletTimeActive;
    private bool _turningToShooter;
    private EnemyHealth _shooter;
    private Vector3 _shooterPosition;

    public float FocusFraction => _focusCapacity > 0f ? _focusRemaining / _focusCapacity : 0f;
    public bool IsInvulnerable => Time.unscaledTime < _invulnerableUntil;

    void Awake()
    {
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_motor == null) _motor = GetComponent<PlayerMotor>();
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        _focusRemaining = _focusCapacity;
        _targetYaw = transform.eulerAngles.y;
        KatanaInputCommandAdapter duplicateInput = GetComponent<KatanaInputCommandAdapter>();
        if (duplicateInput != null) duplicateInput.enabled = false;
        _motor.SetPositionLocked(true);
    }

    void Update()
    {
        if (_player.IsPaused || _player.CurrentState == PlayerActionState.Dead)
            return;

        UpdateFocus();
        if (_turningToShooter)
            UpdateCounterTurn();
        else
            UpdateManualLook();

        if (_shiftHeld && _focusRemaining > 0f && !_bulletTimeActive
            && Time.unscaledTime <= _detectionEndsAt)
            TryBeginExtremeDodge();

        if (_bulletTimeActive && !_turningToShooter
            && (!_shiftHeld || _focusRemaining <= 0f)
            && _counterGraceEndsAt > 0f && Time.unscaledTime >= _counterGraceEndsAt)
            EndBulletTime();
    }

    void OnDisable()
    {
        EndBulletTime();
        if (_motor != null) _motor.SetPositionLocked(false);
    }

    /// <summary>
    /// look과 attackPressed 및 현재 A/D·Shift 입력을 받아 DefenseTest 조작을 갱신한다.
    /// 일반 좌클릭은 기존 공격에, 극한 회피 중 좌클릭은 발사자 반격에 전달한다.
    /// </summary>
    public void HandleInput(Vector2 look, bool attackPressed)
    {
        Keyboard keyboard = Keyboard.current;
        bool left = keyboard != null && keyboard.aKey.isPressed;
        bool right = keyboard != null && keyboard.dKey.isPressed;
        _shiftHeld = keyboard != null
            && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

        if (!_turningToShooter)
        {
            _targetYaw += ((right ? 1f : 0f) - (left ? 1f : 0f))
                * _keyboardDegreesPerSecond * Time.unscaledDeltaTime;
            _targetYaw += look.x * _mouseSensitivity;
        }

        if (!attackPressed) return;
        if (_bulletTimeActive)
        {
            if (!_turningToShooter) BeginCounterTurn();
            return;
        }
        _combat.RequestAttack();
    }

    /// <summary>
    /// Shift 유지와 게임 시간 소모, 실제 시간 회복으로 집중 게이지를 갱신한다.
    /// 새 진입 시 0.3초 판정 창과 공터 범위의 일회성 시야 밝힘을 시작한다.
    /// </summary>
    private void UpdateFocus()
    {
        bool focusing = _shiftHeld && _focusRemaining > 0f;
        if (focusing && !_wasShiftHeld && !_bulletTimeActive)
        {
            _detectionEndsAt = Time.unscaledTime + _detectionWindow;
            SensoryManager sensory = Game.Instance.SensoryManager;
            if (sensory != null)
            {
                SensoryPerceptionMask mask = sensory.GetComponent<SensoryPerceptionMask>();
                if (mask != null) mask.RevealFootstep(transform.position, _focusRevealRadius);
            }
        }

        if (focusing)
            _focusRemaining = Mathf.Max(0f, _focusRemaining - Time.deltaTime);
        else if (!_shiftHeld)
            _focusRemaining = Mathf.Min(_focusCapacity,
                _focusRemaining + _focusCapacity * Time.unscaledDeltaTime / _focusRecoveryDuration);

        if (_bulletTimeActive && !_turningToShooter
            && (!focusing || _focusRemaining <= 0f) && _counterGraceEndsAt <= 0f)
            _counterGraceEndsAt = Time.unscaledTime + _counterGraceDuration;

        _wasShiftHeld = _shiftHeld;
    }

    /// <summary>
    /// 진입 판정 창에 생성된 원거리 적 투사체 중 가장 가까운 것을 찾는다.
    /// 10m 이내 대상이 있으면 발사자를 저장하고 씬의 투사체를 모두 제거한다.
    /// </summary>
    private void TryBeginExtremeDodge()
    {
        EnemyProjectile nearest = null;
        float nearestDistance = _projectileDistance * _projectileDistance;
        EnemyProjectile[] projectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        foreach (EnemyProjectile projectile in projectiles)
        {
            if (!projectile.IsFlying || projectile.gameObject.scene != gameObject.scene
                || projectile.Instigator == null
                || projectile.Instigator.GetComponent<EnemyRangedAttack>() == null)
                continue;
            float distance = (projectile.transform.position - transform.position).sqrMagnitude;
            if (distance > nearestDistance) continue;
            nearest = projectile;
            nearestDistance = distance;
        }
        if (nearest == null) return;

        _shooter = nearest.Instigator.GetComponent<EnemyHealth>();
        _shooterPosition = nearest.Instigator.transform.position;
        foreach (EnemyProjectile projectile in projectiles)
            if (projectile != null && projectile.gameObject.scene == gameObject.scene)
                projectile.Release();

        _invulnerableUntil = Time.unscaledTime + _invulnerabilityDuration;
        _timeScaleBeforeBullet = Time.timeScale;
        _bulletTimeActive = true;
        _counterGraceEndsAt = 0f;
        Time.timeScale = _bulletTimeScale;
    }

    /// <summary>
    /// 현재 시점과 저장한 발사자 위치로 실제 시간 0.5초 회전을 시작한다.
    /// 회전 중에는 수동 시점 입력을 적용하지 않는다.
    /// </summary>
    private void BeginCounterTurn()
    {
        _turningToShooter = true;
        _turnStartedAt = Time.unscaledTime;
        _turnFromYaw = transform.eulerAngles.y;
        _lookVelocity = 0f;
    }

    /// <summary>
    /// 발사자 방향을 실제 시간 진행도로 바라보고 회전 완료 시 참격을 시행한다.
    /// 참격 직후 저장한 시간 배율을 복원한다.
    /// </summary>
    private void UpdateCounterTurn()
    {
        if (_shooter != null && _shooter.IsAlive)
            _shooterPosition = _shooter.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(_shooterPosition - transform.position, Vector3.up);
        float destination = direction.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : _turnFromYaw;
        float progress = Mathf.Clamp01((Time.unscaledTime - _turnStartedAt) / _counterTurnDuration);
        float yaw = Mathf.LerpAngle(_turnFromYaw, destination, Mathf.SmoothStep(0f, 1f, progress));
        _motor.SetHorizontalFacing(yaw);
        _targetYaw = yaw;
        if (progress < 1f) return;
        _turningToShooter = false;
        ExecuteExtremeSlash();
        EndBulletTime();
    }

    /// <summary>
    /// 목표 수평 각도를 실제 시간 기준으로 부드럽게 따라간다.
    /// A/D 또는 마우스 수평 입력이 적용된 시점만 변경한다.
    /// </summary>
    private void UpdateManualLook()
    {
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetYaw,
            ref _lookVelocity, _lookSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        _motor.SetHorizontalFacing(yaw);
    }

    /// <summary>
    /// 검 위치를 시작점으로 일반 검기 5배의 이펙트를 생성한다.
    /// 발사자를 즉사시키고 검 앞 5배 도달 거리와 폭 안의 근거리 적도 즉사시킨다.
    /// </summary>
    private void ExecuteExtremeSlash()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 bladePosition = _bladeBase != null ? _bladeBase.position : transform.position;
        if (_normalSlash != null)
        {
            GameObject effect = Instantiate(_normalSlash.gameObject, bladePosition,
                Quaternion.LookRotation(forward));
            effect.transform.localScale = _normalSlash.transform.localScale * _slashMultiplier;
            effect.SetActive(true);
            ParticleSystem particle = effect.GetComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
            Destroy(effect, 5f);
        }

        _slashVictims.Clear();
        if (_shooter != null && _shooter.IsAlive) _slashVictims.Add(_shooter);
        float swordDistance = Vector3.ProjectOnPlane(bladePosition - transform.position, Vector3.up).magnitude;
        float startDistance = swordDistance + 0.05f;
        float reach = _normalAttackRange * _slashMultiplier;
        float halfWidth = _normalHitRadius * _slashMultiplier;
        Vector3 center = transform.position + forward * (startDistance + reach * 0.5f);
        center.y = bladePosition.y;
        Collider[] hits = Physics.OverlapBox(center,
            new Vector3(halfWidth, halfWidth, reach * 0.5f),
            Quaternion.LookRotation(forward), ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            EnemyHealth health = hit.GetComponentInParent<EnemyHealth>();
            if (health != null && health.IsAlive && health.gameObject.scene == gameObject.scene
                && health.GetComponent<EnemyMeleeAttack>() != null)
                _slashVictims.Add(health);
        }
        foreach (EnemyHealth health in _slashVictims)
        {
            if (health == null || !health.IsAlive) continue;
            Vector3 point = health.transform.position;
            health.TakeDamage(new DamageContext(health.CurrentHealth, gameObject,
                point, forward, "DefensePocExtremeSlash"));
        }
    }

    /// <summary>
    /// 극한 회피의 불릿타임을 종료하고 발동 전 시간 배율을 복원한다.
    /// 입력·카메라 반격 대기 상태도 정리한다.
    /// </summary>
    private void EndBulletTime()
    {
        if (!_bulletTimeActive) return;
        if (Time.timeScale > 0f) Time.timeScale = _timeScaleBeforeBullet;
        _bulletTimeActive = false;
        _turningToShooter = false;
        _counterGraceEndsAt = 0f;
        _detectionEndsAt = 0f;
        _shooter = null;
    }
}
