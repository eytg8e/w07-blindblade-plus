using UnityEngine;

using FMODUnity;

namespace SoundTest
{
    public class BgmPlaybackController : MonoBehaviour
    {
        [Header("BGM")]
        [SerializeField] private EventReference _bgmEvent;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _allowFadeOut = true;
        private FMOD.Studio.EventInstance _instance;

        void OnEnable()
        {
            if (_playOnEnable)
                PlayBgm();
        }

        void Update()
        {
            if (_instance.isValid() && _instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
                ReleaseBgm();
        }

        void OnDisable()
        {
            ReleaseBgm();
        }

        void OnDestroy()
        {
            ReleaseBgm();
        }

        /// <summary>
        /// Inspector의 BGM 이벤트로 2D 인스턴스를 시작한다.
        /// 기존 인스턴스가 재생 또는 정지 중이면 중복 생성하지 않고, 생성한 핸들을 보유한다.
        /// </summary>
        public void PlayBgm()
        {
            if (!isActiveAndEnabled)
                return;
            if (_instance.isValid())
            {
                if (_instance.getPlaybackState(out var state) != FMOD.RESULT.OK || state != FMOD.Studio.PLAYBACK_STATE.STOPPED)
                    return;
                ReleaseBgm();
            }
            if (_bgmEvent.IsNull)
            {
                Debug.LogWarning("[FMOD] Assign a BGM EventReference.", this);
                return;
            }
            try
            {
                var description = RuntimeManager.GetEventDescription(_bgmEvent);
                if (description.is3D(out bool is3D) != FMOD.RESULT.OK || is3D)
                {
                    Debug.LogWarning("[FMOD] Assign a 2D event for BGM playback.", this);
                    return;
                }
                description.isOneshot(out bool isOneShot);
                _instance = RuntimeManager.CreateInstance(_bgmEvent);
                FMOD.RESULT result = _instance.start();
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogError("[FMOD] Failed to start BGM: " + result, this);
                    ReleaseBgm();
                }
            }
            catch (EventNotFoundException exception)
            {
                Debug.LogWarning("[FMOD] BGM event not found in loaded banks: " + exception.Message, this);
                ReleaseBgm();
            }
        }

        /// <summary>
        /// Inspector의 Fade Out 설정으로 BGM을 정지한다.
        /// FMOD의 릴리스 엔벌로프를 허용하며, 완전히 정지한 인스턴스는 Update에서 해제한다.
        /// </summary>
        public void StopBgm()
        {
            if (_instance.isValid())
                _instance.stop(_allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        /// <summary>
        /// 보유 중인 BGM 핸들을 즉시 정지하고 해제한다.
        /// 외부 입력 없이 비활성화 및 파괴 시 재생 자원과 핸들을 정리한다.
        /// </summary>
        private void ReleaseBgm()
        {
            if (!_instance.isValid())
                return;
            _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
            _instance.clearHandle();
        }
    }
}
