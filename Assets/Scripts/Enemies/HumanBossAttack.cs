using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.AI;

/// <summary>인간 보스의 단독·탑승 사격과 탄환 수명 및 반사 적중 알림을 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMotor), typeof(NavMeshAgent))]
public sealed class HumanBossAttack : MonoBehaviour
{
    [Header("사격 연결")]
    [SerializeField] private TwinBossProjectile _projectilePrefab;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private Collider _hitCollider;
    [SerializeField, Min(0f)] private float _damage = 15f;
    [SerializeField, Min(0.1f)] private float _preparation = 0.8f;
    [SerializeField, Min(0.1f)] private float _shotInterval = 0.8f;
    [SerializeField, Min(1)] private int _soloShots = 3;
    [SerializeField] private SoundId _preparationSound = SoundId.EnemyRangedAttackVoice;
    private readonly List<TwinBossProjectile> _projectiles = new List<TwinBossProjectile>();
    private EnemyHealth _health;
    private EnemyMotor _motor;
    private NavMeshAgent _agent;
    private KatanaDamageReceiver _player;
    private PlayerMotor _playerMotor;
    private Coroutine _shooting;

    [Header("단독 사격 순간이동")]
    [SerializeField, Min(1f), Tooltip("플레이어 주변 순간이동 반경(미터)")]
    private float _teleportRadius = 6f;
    [SerializeField, Range(1f, 180f), Tooltip("플레이어 수평 시점 기준 등장 각도(도)")]
    private float _teleportArc = 180f;
    [SerializeField] private LayerMask _teleportObstacleMask = Physics.DefaultRaycastLayers;
    private readonly Collider[] _teleportContacts = new Collider[16];
    private NavMeshPath _teleportPath;

    [Header("탑승 상태")]
    [SerializeField, Min(0.5f)] private float _dismountDistance = 2f;
    private readonly Collider[] _dismountContacts = new Collider[16];
    private Transform _seat;
    private bool _mounted;
    private Vector3 _navigationPosition;
    private bool _previousUpdatePosition;
    private bool _previousUpdateRotation;

    public EnemyHealth Health => _health;
    public bool IsAttacking => _shooting != null;
    public bool HasProjectiles => _projectiles.Count > 0;
    public bool IsReady => _motor.IsReady;
    public bool IsMounted => _mounted;
    public bool IsConfigured => _projectilePrefab != null && _muzzle != null && _hitCollider != null;
    public event Action ShotPreparing;
    public event Action ShotFired;
    public event Action ReflectedHit;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _motor = GetComponent<EnemyMotor>();
        _agent = GetComponent<NavMeshAgent>();
        _teleportPath = new NavMeshPath();
    }

    void LateUpdate()
    {
        if (_seat != null && _health.IsAlive)
            transform.SetPositionAndRotation(_seat.position, _seat.rotation);
    }

    /// <summary>player에게 continuous 연사 또는 설정 횟수 사격을 시작하며 teleportBetweenShots로 발사 사이 순간이동을 제어한다.</summary>
    public void StartShooting(KatanaDamageReceiver player, bool continuous, bool teleportBetweenShots = true)
    {
        StopShooting();
        _player = player;
        _playerMotor = player.GetComponent<PlayerMotor>();
        _motor.Stop();
        _shooting = StartCoroutine(Shoot(continuous, teleportBetweenShots));
    }

    /// <summary>continuous로 전조·발사·휴식을 반복하고 teleportBetweenShots이면 단독 사격 사이에 순간이동하며 실행 참조를 정리한다.</summary>
    private IEnumerator Shoot(bool continuous, bool teleportBetweenShots)
    {
        yield return null;
        int shots = 0;
        while (_health.IsAlive && _player != null && _player.IsAlive && (continuous || shots < _soloShots))
        {
            ShotPreparing?.Invoke();
            if (_preparationSound != SoundId.None)
                Managers.Sound.Play(_preparationSound, transform.position, 1f, gameObject);
            yield return new WaitForSeconds(_preparation);
            if (!_health.IsAlive || _player == null || !_player.IsAlive) break;
            Vector3 direction = (_player.transform.position + Vector3.up - _muzzle.position).normalized;
            if (direction.sqrMagnitude > 0.000001f)
            {
                Vector3 facing = Vector3.ProjectOnPlane(direction, Vector3.up);
                if (!_mounted && facing.sqrMagnitude > 0.000001f)
                    transform.rotation = Quaternion.LookRotation(facing);
                direction = (_player.transform.position + Vector3.up - _muzzle.position).normalized;
                TwinBossProjectile projectile = Instantiate(_projectilePrefab, _muzzle.position,
                    Quaternion.LookRotation(direction));
                projectile.ReflectedHit += HandleReflectedHit;
                projectile.Released += HandleProjectileReleased;
                _projectiles.Add(projectile);
                projectile.Initialize(_health, _hitCollider, _player, _damage, direction);
                ShotFired?.Invoke();
            }
            shots++;
            yield return new WaitForSeconds(_shotInterval);
            if (teleportBetweenShots && !continuous && !_mounted && shots < _soloShots
                && _health.IsAlive && _player != null && _player.IsAlive && _motor.IsReady)
                TryTeleportAroundPlayer();
        }
        _shooting = null;
    }

    /// <summary>플레이어 전방 주변에서 빈 NavMesh 지면과 완전한 경로를 확인해 순간이동하며 성공 여부를 반환한다.</summary>
    private bool TryTeleportAroundPlayer()
    {
        Vector3 forward = _playerMotor.ViewForward;
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = _agent.areaMask
        };
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = UnityEngine.Random.Range(-_teleportArc * 0.5f, _teleportArc * 0.5f);
            Vector3 desired = _player.transform.position
                + Quaternion.Euler(0f, angle, 0f) * forward * _teleportRadius;
            desired.y = _agent.nextPosition.y;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 0.5f, filter)) continue;
            Vector3 position = hit.position;
            if (Vector3.Dot(position - _player.transform.position, forward) < 0f
                || (position - transform.position).sqrMagnitude < 0.25f) continue;
            float radius = _agent.radius;
            int count = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (radius + 0.05f),
                position + Vector3.up * Mathf.Max(radius + 0.05f, _agent.height - radius),
                radius, _teleportContacts, _teleportObstacleMask, QueryTriggerInteraction.Ignore);
            bool blocked = count == _teleportContacts.Length;
            for (int i = 0; i < count; i++)
            {
                if (!_teleportContacts[i].transform.IsChildOf(transform)) blocked = true;
            }
            if (blocked || !_agent.CalculatePath(position, _teleportPath)
                || _teleportPath.status != NavMeshPathStatus.PathComplete) continue;
            _motor.Stop();
            if (!_agent.Warp(position)) continue;
            Vector3 facing = Vector3.ProjectOnPlane(_player.transform.position - position, Vector3.up);
            if (facing.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(facing);
            return true;
        }
        return false;
    }

    /// <summary>seat를 탑승 위치로 저장하고 인간의 NavMesh 위치·회전 갱신을 잠시 중단한다.</summary>
    public void Mount(Transform seat)
    {
        _motor.Stop();
        _navigationPosition = _agent.nextPosition;
        _previousUpdatePosition = _agent.updatePosition;
        _previousUpdateRotation = _agent.updateRotation;
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _seat = seat;
        _mounted = true;
        transform.SetPositionAndRotation(seat.position, seat.rotation);
    }

    /// <summary>탑승을 끝내고 현재 위치 주변 또는 탑승 전 지면으로 복귀해 NavMesh 갱신을 복원한다.</summary>
    public void Dismount()
    {
        if (!_mounted) return;
        _mounted = false;
        Vector3 right = _seat != null ? _seat.right : transform.right;
        _seat = null;
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = _agent.areaMask
        };
        Vector3 destination = _navigationPosition;
        for (int side = 1; side >= -1; side -= 2)
        {
            Vector3 desired = transform.position + right * (_dismountDistance * side);
            desired.y = _navigationPosition.y;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 1f, filter)) continue;
            float radius = _agent.radius;
            int count = Physics.OverlapCapsuleNonAlloc(hit.position + Vector3.up * (radius + 0.05f),
                hit.position + Vector3.up * Mathf.Max(radius + 0.05f, _agent.height - radius),
                radius, _dismountContacts, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool blocked = count == _dismountContacts.Length;
            for (int i = 0; i < count; i++)
            {
                if (!_dismountContacts[i].transform.IsChildOf(transform)) blocked = true;
            }
            if (blocked) continue;
            destination = hit.position;
            break;
        }
        _agent.updatePosition = _previousUpdatePosition;
        _agent.updateRotation = _previousUpdateRotation;
        if (_agent.isActiveAndEnabled) _agent.Warp(destination);
    }

    /// <summary>사격 코루틴을 중지하고 현재 공격의 모든 탄환과 이벤트 구독을 정리한다.</summary>
    public void StopShooting()
    {
        if (_shooting != null) StopCoroutine(_shooting);
        _shooting = null;
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            TwinBossProjectile projectile = _projectiles[i];
            if (projectile == null) continue;
            projectile.ReflectedHit -= HandleReflectedHit;
            projectile.Released -= HandleProjectileReleased;
            projectile.Release();
        }
        _projectiles.Clear();
    }

    /// <summary>projectile의 반사 인간 적중을 협력 패턴 제어자에게 전달한다.</summary>
    private void HandleReflectedHit(TwinBossProjectile projectile)
    {
        ReflectedHit?.Invoke();
    }

    /// <summary>반환된 projectile의 구독을 해제하고 관리 목록에서 제거한다.</summary>
    private void HandleProjectileReleased(TwinBossProjectile projectile)
    {
        projectile.ReflectedHit -= HandleReflectedHit;
        projectile.Released -= HandleProjectileReleased;
        _projectiles.Remove(projectile);
    }

    void OnDisable()
    {
        StopShooting();
        Dismount();
    }
}
