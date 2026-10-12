using UnityEngine;

using UnityEngine.AI;

/// <summary>쌍둥이의 지상 이동 상태를 공용 발자국 생성기와 발소리에 연결한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(NavMeshAgent), typeof(FootstepEmitter))]
public sealed class TwinBossFootsteps : MonoBehaviour
{
    [Header("발소리")]
    [SerializeField, Min(0f)] private float _volume = 1.7782794f;
    private NavMeshAgent _agent;
    private FootstepEmitter _emitter;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _emitter = GetComponent<FootstepEmitter>();
    }

    void OnEnable()
    {
        _emitter.OnFootstepEmitted += HandleFootstep;
    }

    void LateUpdate()
    {
        _emitter.enabled = _agent.isActiveAndEnabled && _agent.isOnNavMesh && _agent.updatePosition;
    }

    /// <summary>character의 지면 접촉 position에 공용 적 발소리를 설정된 음량으로 재생한다.</summary>
    private void HandleFootstep(Transform character, Vector3 position)
    {
        SoundId sound = EnemySoundSettings.GetSound(EnemySoundAction.Footstep, EnemySoundStage.Action);
        if (sound != SoundId.None)
            Managers.Sound.Play(sound, position, volume: _volume, owner: gameObject, playDuringMovement: true);
    }

    void OnDisable()
    {
        _emitter.OnFootstepEmitted -= HandleFootstep;
        _emitter.enabled = false;
    }
}
