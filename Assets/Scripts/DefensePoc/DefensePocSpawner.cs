using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DefenseTest 원형 공터 가장자리에서 근거리와 원거리 적을 주기적으로 생성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DefensePocSpawner : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Transform _player;
    [SerializeField] private EnemyHealth _meleePrefab;
    [SerializeField] private EnemyHealth _rangedPrefab;

    [Header("생성 규칙")]
    [SerializeField, Min(0f)] private float _spawnRadius = 19f;
    [SerializeField, Min(0.01f)] private float _minimumInterval = 1f;
    [SerializeField, Min(0.01f)] private float _maximumInterval = 2f;
    [SerializeField, Min(1)] private int _maximumAlive = 2;
    [SerializeField, Min(1)] private int _meleeWeight = 4;
    [SerializeField, Min(1)] private int _rangedWeight = 1;
    [SerializeField, Min(0.1f)] private float _meleeAttackRange = 1.2f;
    private readonly List<EnemyHealth> _spawned = new List<EnemyHealth>();
    private float _nextSpawnIn;

    void OnEnable()
    {
        _nextSpawnIn = Random.Range(_minimumInterval, _maximumInterval);
    }

    void Update()
    {
        for (int index = _spawned.Count - 1; index >= 0; index--)
        {
            EnemyHealth enemy = _spawned[index];
            if (enemy != null && enemy.IsAlive) continue;
            _spawned.RemoveAt(index);
            if (enemy != null) Destroy(enemy.gameObject);
        }

        if (_player == null || _spawned.Count >= _maximumAlive) return;
        _nextSpawnIn -= Time.deltaTime;
        if (_nextSpawnIn > 0f) return;
        if (TrySpawnEnemy())
            _nextSpawnIn = Random.Range(_minimumInterval, _maximumInterval);
    }

    /// <summary>
    /// 공터 가장자리의 NavMesh 지점을 찾고 4:1 가중치로 고른 적을 생성한다.
    /// 생성한 적은 플레이어를 거리와 장애물에 관계없이 추적하도록 설정한다.
    /// </summary>
    private bool TrySpawnEnemy()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 desired = _player.position + direction * _spawnRadius;
        if (!NavMesh.SamplePosition(desired, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            return false;

        int roll = Random.Range(0, _meleeWeight + _rangedWeight);
        EnemyHealth prefab = roll < _meleeWeight ? _meleePrefab : _rangedPrefab;
        EnemyHealth enemy = Instantiate(prefab, navHit.position,
            Quaternion.LookRotation((_player.position - navHit.position).normalized));
        EnemySensor sensor = enemy.GetComponent<EnemySensor>();
        sensor.SetTarget(_player);
        sensor.SetAlwaysTrackTarget(true);
        if (prefab == _meleePrefab)
        {
            enemy.GetComponent<EnemyAttackSelector>().SetMeleeOnly(true);
            enemy.GetComponent<EnemyMeleeAttack>().SetAttackRange(_meleeAttackRange);
        }
        _spawned.Add(enemy);
        return true;
    }
}
