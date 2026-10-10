using UnityEngine;

using BlindBlade.Rendering;
using BlindBlade.Enemies;

/// <summary>
/// 메인맵의 플레이어를 공통 감각 관측자로 연결하고 이동 발소리를 전달한다.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class MainMapSession : MonoBehaviour
{
    [Header("플레이어 연결")]
    [SerializeField] private PlayerController _player;

    private SensoryPerceptionMask _perceptionMask;

    void Start()
    {
        ConnectCombatService();
        Game.Instance.SensoryManager.SetObserver(_player.transform);
        if (!Game.Instance.SensoryManager.IsReady)
            Game.Instance.SensoryManager.Initialize();
        _perceptionMask = Game.Instance.SensoryManager.GetComponent<SensoryPerceptionMask>();
        _perceptionMask.SetFootstepOnly(false);
        Game.Instance.SensoryManager.SetEffectsVisible(true);
        Game.Instance.SensoryManager.SetFootstepsSuppressed(false);
        Managers.Sound.SetFootstepOnlyOwner(null);
    }

    /// <summary>
    /// 세션과 같은 씬의 기존 전투 서비스를 찾아 플레이어와 상호 연결한다.
    /// 씬별 수동 참조 없이 감지와 맵 서비스는 유지하며 서비스를 찾지 못하면 오류를 알린다.
    /// </summary>
    private void ConnectCombatService()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            EnemyCombatService service = root.GetComponentInChildren<EnemyCombatService>();
            if (service == null)
                continue;
            service.ConfigurePlayer(_player.transform);
            _player.ConfigureCombatService(service);
            return;
        }
        Debug.LogError("현재 씬에 EnemyCombatService가 없어 플레이어 공격 판정을 연결할 수 없습니다.", this);
    }

    void OnEnable()
    {
        _player.FootstepEmitted += HandlePlayerFootstep;
        if (_perceptionMask != null)
        {
            Game.Instance.SensoryManager.SetObserver(_player.transform);
            if (!Game.Instance.SensoryManager.IsReady)
                Game.Instance.SensoryManager.Initialize();
            _perceptionMask.SetFootstepOnly(false);
            _perceptionMask.GetComponent<SensoryManager>().SetEffectsVisible(true);
            _perceptionMask.GetComponent<SensoryManager>().SetFootstepsSuppressed(false);
            Managers.Sound.SetFootstepOnlyOwner(null);
        }
    }

    void OnDisable()
    {
        if (_player != null)
            _player.FootstepEmitted -= HandlePlayerFootstep;
        if (_perceptionMask != null)
        {
            _perceptionMask.SetFootstepOnly(false);
            _perceptionMask.GetComponent<SensoryManager>().SetEffectsVisible(true);
            _perceptionMask.GetComponent<SensoryManager>().SetFootstepsSuppressed(false);
        }
        if (Managers.IsInitialized)
            Managers.Sound.SetFootstepOnlyOwner(null);
    }

    /// <summary>
    /// step의 지면 위치와 소음 강도를 사용하여 기존 Footstep 소리를 한 번 요청한다.
    /// 재생 중 소리는 플레이어를 따라가며 주변 시야는 기존 상시 인지 설정을 사용한다.
    /// </summary>
    private void HandlePlayerFootstep(PlayerFootstepEvent step)
    {
        Managers.Sound.PlayAttached(SoundId.Footstep, _player.gameObject, step.NoiseStrength);
    }
}
