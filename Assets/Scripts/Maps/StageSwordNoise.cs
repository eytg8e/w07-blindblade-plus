using UnityEngine;
using UnityEngine.AI;

using BlindBlade.Enemies;

/// <summary>같은 스테이지의 연결된 복도에 검 휘두르기 소음을 전달한다.</summary>
public sealed class StageSwordNoise : MonoBehaviour
{
    [Header("스테이지 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private EnemyController[] _enemies;

    [Header("검 소음")]
    [SerializeField, Min(1f)] private float _hearingDistance = 40f;
    [SerializeField, Min(0.1f)] private float _alertDuration = 15f;
    private int _lastActionId = -1;

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
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
