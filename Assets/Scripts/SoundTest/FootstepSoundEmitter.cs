using UnityEngine;

namespace SoundTest
{
    public class FootstepSoundEmitter : MonoBehaviour
    {
        [Header("Footstep")]
        [SerializeField] private SoundId _soundId = SoundId.Footstep;
        private int _lastStepFrame = -1;

        [Header("Source Marker")]
        [SerializeField] private Transform _sourceMarker;
        [SerializeField, Min(0.01f)] private float _markerDuration = 0.2f;
        private float _markerEndTime;

        void Update()
        {
            _sourceMarker.localScale = Vector3.one * (Time.time < _markerEndTime ? 0.8f : 0.4f);
        }

        void OnDisable()
        {
            if (Managers.IsInitialized)
                Managers.Sound.StopFor(gameObject);
            _lastStepFrame = -1;
            _markerEndTime = 0f;
            _sourceMarker.localScale = Vector3.one * 0.4f;
        }

        /// <summary>
        /// Inspector의 발소리 ID를 Managers.Sound에 전달하여 현재 GameObject에 연결된 단발음을 재생한다.
        /// 같은 프레임의 중복 요청을 무시하고 재생이 성공하면 진원지 Sphere를 잠시 확대한다.
        /// </summary>
        public void PlayFootstep()
        {
            if (!isActiveAndEnabled || _lastStepFrame == Time.frameCount)
                return;
            _lastStepFrame = Time.frameCount;
            if (Managers.Sound.PlayAttached(_soundId, gameObject))
                _markerEndTime = Time.time + _markerDuration;
        }
    }
}
