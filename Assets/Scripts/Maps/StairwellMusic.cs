using UnityEngine;

using FMODUnity;

/// <summary>안전한 계단 씬에서 지정한 FMOD BGM을 재생하고 씬을 떠날 때 해제한다.</summary>
public sealed class StairwellMusic : MonoBehaviour
{
    [Header("계단 BGM (2D 루프 FMOD 이벤트)")]
    [SerializeField] private EventReference _fmodEvent;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;
    private FMOD.Studio.EventInstance _instance;

    void OnEnable()
    {
        if (!_fmodEvent.IsNull)
        {
            _instance = RuntimeManager.CreateInstance(_fmodEvent);
            _instance.setVolume(_volume);
            _instance.start();
        }
    }

    void OnDisable()
    {
        if (_instance.isValid())
        {
            _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
            _instance.clearHandle();
        }
    }
}
