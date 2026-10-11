using System.Collections.Generic;

using UnityEngine;

/// <summary>현재 플레이어의 감각·발소리·검 소음 연결을 씬 수명 동안 관리한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StageSwordNoise))]
public sealed class MainMapSession : MonoBehaviour
{
    [Header("세션 연결")]
    private PlayerController _player;
    private StageSwordNoise _swordNoise;

    /// <summary>
    /// Game.Player를 감각 관측자로 등록하고 발소리 및 선택적인 검 소음을 연결한다.
    /// useSwordNoise와 hearingDistance·alertDuration으로 소음 감지 규칙을 설정한다.
    /// </summary>
    public void Initialize(bool useSwordNoise, float hearingDistance, float alertDuration)
    {
        Shutdown();
        _player = Game.Player;
        SensoryManager sensory = Game.Instance.SensoryManager;
        sensory.SetObserver(_player.transform);
        if (!sensory.IsReady) sensory.Initialize();
        SensoryPerceptionMask mask = sensory.GetComponent<SensoryPerceptionMask>();
        mask.enabled = true;
        mask.SetFootstepOnly(false);
        sensory.SetEffectsVisible(true);
        sensory.SetFootstepsSuppressed(false);
        Managers.Sound.SetFootstepOnlyOwner(null);
        _player.FootstepEmitted += HandlePlayerFootstep;
        _swordNoise = GetComponent<StageSwordNoise>();
        if (!useSwordNoise) return;
        var enemies = new List<EnemyController>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            enemies.AddRange(root.GetComponentsInChildren<EnemyController>(true));
        _swordNoise.Initialize(_player, enemies.ToArray(), hearingDistance, alertDuration);
    }

    /// <summary>step의 소음 강도로 세션 플레이어에 붙은 발소리를 재생한다. 이동·공격 판정은 변경하지 않는다.</summary>
    private void HandlePlayerFootstep(PlayerFootstepEvent step)
    {
        Managers.Sound.PlayAttached(SoundId.Footstep, _player.gameObject, step.NoiseStrength);
    }

    /// <summary>발소리·검 소음 구독과 현재 플레이어 참조를 해제한다. 기존 매니저의 인스턴스는 유지한다.</summary>
    public void Shutdown()
    {
        if (_player != null) _player.FootstepEmitted -= HandlePlayerFootstep;
        if (_swordNoise != null) _swordNoise.Shutdown();
        if (Managers.IsInitialized) Managers.Sound.SetFootstepOnlyOwner(null);
        _player = null;
    }

    void OnDisable()
    {
        Shutdown();
    }
}
