using UnityEngine;
using UnityEngine.AI;

using BlindBlade.Enemies;

/// <summary>같은 스테이지의 연결된 복도에 검 휘두르기 소음을 전달한다.</summary>
public sealed class StageSwordNoise : MonoBehaviour
{
    private EnemyController[] _enemies;
    private PlayerController _player;

    private float _hearingDistance;
    private float _alertDuration;
    private int _lastActionId = -1;

    /// <summary>player와 현재 씬 enemies를 연결하고 hearingDistance 및 alertDuration으로 검 소음 전달을 준비한다.</summary>
    public void Initialize(PlayerController player, EnemyController[] enemies, float hearingDistance, float alertDuration)
    {
        Shutdown();
        _player = player;
        _enemies = enemies;
        _hearingDistance = hearingDistance;
        _alertDuration = alertDuration;
        _lastActionId = -1;
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        Shutdown();
    }

    /// <summary>현재 플레이어의 검 행동 구독을 해제하고 씬 참조를 정리한다.</summary>
    public void Shutdown()
    {
        if (_player != null)
            _player.ActionEvent -= HandleAction;
        _player = null;
        _enemies = System.Array.Empty<EnemyController>();
    }

    /// <summary>일반 베기 시작 또는 발도 베기 action의 소음을 행동당 한 번 복도에 전달한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        bool swing = (action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted;
        if (!swing || action.ActionId == _lastActionId)
            return;
        _lastActionId = action.ActionId;
        AlertEnemies(_player.transform.position);
    }

    /// <summary>position까지 연결된 NavMesh 경로 길이가 청각 범위 이내인 생존 적에게 소음 위치를 전달한다.</summary>
    public void AlertEnemies(Vector3 position)
    {
        foreach (EnemyController enemy in _enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.gameObject.scene != gameObject.scene)
                continue;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!agent.isOnNavMesh || !NavMesh.SamplePosition(position, out NavMeshHit destination, 1f, filter))
                continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(agent.nextPosition, destination.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete)
                continue;
            float distance = 0f;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                distance += Vector3.Distance(corners[i - 1], corners[i]);
            if (distance <= _hearingDistance)
                enemy.HearNoise(destination.position, _alertDuration);
        }
    }
}
