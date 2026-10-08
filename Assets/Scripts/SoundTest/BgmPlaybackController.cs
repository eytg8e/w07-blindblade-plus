using UnityEngine;

namespace SoundTest
{
    public class BgmPlaybackController : MonoBehaviour
    {
        [Header("BGM")]
        [SerializeField] private SoundId _bgmId = SoundId.Ambience;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _allowFadeOut = true;

        void OnEnable()
        {
            if (_playOnEnable)
                PlayBgm();
        }

        void OnDisable()
        {
            if (Managers.IsInitialized)
                Managers.Sound.StopFor(gameObject);
        }

        /// <summary>
        /// Inspector의 BGM ID를 Managers.Sound에 전달하여 재생한다.
        /// 활성 컴포넌트의 GameObject를 소유자로 지정하며 인스턴스 관리는 매니저에 맡긴다.
        /// </summary>
        public void PlayBgm()
        {
            if (isActiveAndEnabled)
                Managers.Sound.PlayBgm(_bgmId, gameObject);
        }

        /// <summary>
        /// Inspector의 Fade Out 설정으로 이 GameObject가 소유한 BGM을 정지한다.
        /// Managers.Sound를 사용하며 다른 소유자의 사운드는 유지한다.
        /// </summary>
        public void StopBgm()
        {
            if (Managers.IsInitialized)
                Managers.Sound.StopFor(gameObject, _allowFadeOut);
        }
    }
}
