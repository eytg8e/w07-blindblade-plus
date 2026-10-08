using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

namespace SoundTest
{
    public class FootstepSoundEmitter : MonoBehaviour
    {
        [Header("Footstep")]
        [SerializeField] private EventReference _footstepEvent;
        private readonly List<FMOD.Studio.EventInstance> _instances = new List<FMOD.Studio.EventInstance>();
        private int _lastStepFrame = -1;

        [Header("Source Marker")]
        [SerializeField] private Transform _sourceMarker;
        [SerializeField, Min(0.01f)] private float _markerDuration = 0.2f;
        private float _markerEndTime;

        void Update()
        {
            _sourceMarker.localScale = Vector3.one * (Time.time < _markerEndTime ? 0.8f : 0.4f);
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                var instance = _instances[i];
                if (!instance.isValid() || (instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state == FMOD.Studio.PLAYBACK_STATE.STOPPED))
                {
                    ReleaseFootstep(instance);
                    _instances.RemoveAt(i);
                }
            }
        }

        void OnDisable()
        {
            foreach (var instance in _instances)
                ReleaseFootstep(instance);
            _instances.Clear();
            _lastStepFrame = -1;
            _sourceMarker.localScale = Vector3.one * 0.4f;
        }

        /// <summary>
        /// Inspector의 3D 단발 이벤트를 현재 GameObject에 연결하여 한 번 재생한다.
        /// 같은 프레임의 중복 호출은 무시하며, 재생 중 위치는 RuntimeManager가 추적하고 진원지 Sphere를 잠시 확대한다.
        /// </summary>
        public void PlayFootstep()
        {
            if (!isActiveAndEnabled || _lastStepFrame == Time.frameCount)
                return;
            _lastStepFrame = Time.frameCount;
            if (_footstepEvent.IsNull)
            {
                Debug.LogWarning("[FMOD] Assign a footstep EventReference.", this);
                return;
            }
            try
            {
                var description = RuntimeManager.GetEventDescription(_footstepEvent);
                if (description.is3D(out bool is3D) != FMOD.RESULT.OK || !is3D || description.isOneshot(out bool isOneShot) != FMOD.RESULT.OK || !isOneShot)
                {
                    Debug.LogWarning("[FMOD] Assign a 3D one-shot event for footsteps.", this);
                    return;
                }
                var instance = RuntimeManager.CreateInstance(_footstepEvent);
                RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
                FMOD.RESULT result = instance.start();
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogError("[FMOD] Failed to start footstep playback: " + result, this);
                    ReleaseFootstep(instance);
                    return;
                }
                _instances.Add(instance);
                _markerEndTime = Time.time + _markerDuration;
            }
            catch (EventNotFoundException exception)
            {
                Debug.LogWarning("[FMOD] Footstep event not found in loaded banks: " + exception.Message, this);
            }
        }

        /// <summary>
        /// 전달받은 instance를 발음체에서 분리하고 즉시 정지 및 해제한다.
        /// 재생 종료 또는 오브젝트 비활성화 시 FMOD 자원을 반환한다.
        /// </summary>
        private void ReleaseFootstep(FMOD.Studio.EventInstance instance)
        {
            if (!instance.isValid())
                return;
            RuntimeManager.DetachInstanceFromGameObject(instance);
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }
    }
}
