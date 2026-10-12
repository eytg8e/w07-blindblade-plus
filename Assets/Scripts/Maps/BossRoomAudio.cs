using System.Collections;

using UnityEngine;

using UnityEngine.SceneManagement;

/// <summary>보스방 씬 수명에 BGM을 연결하고 재입장·재시작마다 처음부터 재생한다.</summary>
[DisallowMultipleComponent]
public sealed class BossRoomAudio : MonoBehaviour
{
    [Header("보스방 BGM")]
    [SerializeField] private SoundId _music = SoundId.BossBgm;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;
    private Coroutine _routine;
    private bool _started;

    void Start()
    {
        _started = true;
        _routine = StartCoroutine(PlayRoomMusic());
    }

    void OnEnable()
    {
        if (_started) _routine = StartCoroutine(PlayRoomMusic());
    }

    void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (!Managers.IsInitialized) return;
        bool owned = Managers.Sound.IsBgmOwnedBy(gameObject);
        Managers.Sound.StopFor(gameObject);
        if (!owned || !Game.IsInitialized || Game.Player == null) return;
        PlayerController player = Game.Player;
        if (!player.gameObject.activeInHierarchy || player.CurrentState == PlayerActionState.Dead
            || player.gameObject.scene != gameObject.scene || SceneManager.GetActiveScene() != gameObject.scene) return;
        CombatSoundProfile profile = Resources.Load<CombatSoundProfile>(CombatSoundProfile.RESOURCE_PATH);
        if (profile != null && profile.PlayAmbience)
            Managers.Sound.PlayBgm(profile.AmbienceSound, player.gameObject, profile.LoopAmbience, profile.AmbienceVolume);
    }

    /// <summary>
    /// 세션과 플레이어 초기화를 기다리고 기존 BGM을 종료한 뒤 지정 음악을 처음부터 반복 재생한다.
    /// _music과 _volume을 사용하며 BGM 소유자를 현재 씬 오브젝트로 설정한다.
    /// </summary>
    private IEnumerator PlayRoomMusic()
    {
        yield return null;
        while (!Managers.IsInitialized || !Game.IsInitialized || Game.Player == null) yield return null;
        Managers.Sound.StopBgm(false);
        Managers.Sound.PlayBgm(_music, gameObject, true, _volume);
        _routine = null;
    }
}
