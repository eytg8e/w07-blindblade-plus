using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

public class SoundManager
{
    [Header("Configuration")]
    private readonly SoundCatalog _catalog;

    [Header("Effects")]
    private readonly List<FMOD.Studio.EventInstance> _effects = new List<FMOD.Studio.EventInstance>();

    [Header("BGM")]
    private FMOD.Studio.EventInstance _bgm;
    private SoundId _bgmId = SoundId.None;
    private bool _bgmReleaseRequested;

    [Header("Mixer")]
    private FMOD.Studio.Bus _bgmBus;
    private FMOD.Studio.Bus _effectBus;
    private bool _busesReady;
    private float _bgmVolume = 1f;
    private float _effectVolume = 1f;

    /// <summary>
    /// catalog의 사운드 ID와 FMOD 이벤트 연결을 보관한다.
    /// FMOD 재생 시스템은 실제 재생 요청 시 초기화하며, 매니저의 설정 상태를 준비한다.
    /// </summary>
    internal SoundManager(SoundCatalog catalog)
    {
        _catalog = catalog;
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            if (RuntimeManager.IsInitialized && TryInitializeBuses())
                _bgmBus.setVolume(_bgmVolume);
        }
    }

    public float EffectVolume
    {
        get => _effectVolume;
        set
        {
            _effectVolume = Mathf.Clamp01(value);
            if (RuntimeManager.IsInitialized && TryInitializeBuses())
                _effectBus.setVolume(_effectVolume);
        }
    }

    /// <summary>
    /// soundId의 단발 효과음을 position에서 volume 배율로 재생한다.
    /// 2D 이벤트는 위치를 사용하지 않으며, 재생에 성공하면 true를 반환한다.
    /// </summary>
    public bool Play(SoundId soundId, Vector3 position = default, float volume = 1f)
    {
        return PlayEffect(soundId, position, null, volume);
    }

    /// <summary>
    /// soundId의 단발 효과음을 source에 연결하여 volume 배율로 재생한다.
    /// 재생 중 source의 위치를 추적하며, 유효한 오브젝트에서 재생에 성공하면 true를 반환한다.
    /// </summary>
    public bool PlayAttached(SoundId soundId, GameObject source, float volume = 1f)
    {
        if (source == null)
        {
            Debug.LogWarning("[SoundManager] PlayAttached requires a source GameObject.");
            return false;
        }
        return PlayEffect(soundId, source.transform.position, source, volume);
    }

    /// <summary>
    /// soundId의 2D BGM을 재생하고 다른 곡이면 이전 BGM을 즉시 정지한다.
    /// 같은 BGM은 중복 생성하지 않으며, 재생 요청이 유효하면 true, 페이드 정지 중이면 false를 반환한다.
    /// </summary>
    public bool PlayBgm(SoundId soundId)
    {
        if (!TryResolveEvent(soundId, SoundType.BGM, out EventReference eventReference))
            return false;
        if (_bgm.isValid() && _bgmId == soundId && _bgm.getPlaybackState(out var state) == FMOD.RESULT.OK && state != FMOD.Studio.PLAYBACK_STATE.STOPPED)
            return state != FMOD.Studio.PLAYBACK_STATE.STOPPING;
        try
        {
            var description = RuntimeManager.GetEventDescription(eventReference);
            if (description.is3D(out bool is3D) != FMOD.RESULT.OK || is3D)
            {
                Debug.LogWarning("[SoundManager] BGM requires a 2D event: " + soundId);
                return false;
            }
            StopBgm(false);
            _bgm = RuntimeManager.CreateInstance(eventReference);
            _bgmId = soundId;
            _bgmReleaseRequested = false;
            FMOD.RESULT result = _bgm.start();
            if (result == FMOD.RESULT.OK)
                return true;
            Debug.LogError("[SoundManager] Failed to start BGM: " + soundId + ", " + result);
            StopBgm(false);
        }
        catch (EventNotFoundException exception)
        {
            Debug.LogWarning("[SoundManager] BGM event not found in loaded banks: " + exception.Message);
        }
        return false;
    }

    /// <summary>
    /// allowFadeOut에 따라 BGM을 페이드 또는 즉시 정지하고 해제를 요청한다.
    /// 페이드 중 핸들은 중복 재생 방지와 Clear의 즉시 정지를 위해 보관하며 FMOD가 종료 후 자원을 반환한다.
    /// </summary>
    public void StopBgm(bool allowFadeOut = true)
    {
        if (_bgm.isValid() && RuntimeManager.IsInitialized)
        {
            _bgm.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
            if (!_bgmReleaseRequested)
            {
                _bgm.release();
                _bgmReleaseRequested = true;
            }
            if (allowFadeOut)
                return;
        }
        _bgm.clearHandle();
        _bgmId = SoundId.None;
        _bgmReleaseRequested = false;
    }

    /// <summary>
    /// 외부 입력 없이 이 매니저가 재생한 단발 효과음을 모두 즉시 정지한다.
    /// 시작 시 요청한 해제가 정지 후 완료되며, 보관한 효과음 핸들 목록을 비운다.
    /// </summary>
    public void StopEffects()
    {
        if (RuntimeManager.IsInitialized)
        {
            foreach (var instance in _effects)
            {
                if (!instance.isValid())
                    continue;
                RuntimeManager.DetachInstanceFromGameObject(instance);
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
        }
        _effects.Clear();
    }

    /// <summary>
    /// type이 BGM이면 음악을 페이드 정지하고 Effect이면 효과음을 즉시 정지한다.
    /// 지정된 종류의 매니저 재생 상태를 정리한다.
    /// </summary>
    public void Stop(SoundType type = SoundType.Effect)
    {
        if (type == SoundType.BGM)
            StopBgm();
        else
            StopEffects();
    }

    /// <summary>
    /// 외부 입력 없이 보유한 BGM과 효과음을 즉시 정지하고 정리한다.
    /// Bank와 다른 컴포넌트의 인스턴스는 유지하며 다음 요청에서 Bus 연결을 다시 확인한다.
    /// </summary>
    public void Clear()
    {
        StopBgm(false);
        StopEffects();
        _busesReady = false;
    }

    /// <summary>
    /// soundId의 단발 효과음을 position 또는 source에서 volume 배율로 재생한다.
    /// 루프 이벤트는 거부하며 성공 시 해제를 요청한 핸들을 보관하고 true를 반환한다.
    /// </summary>
    private bool PlayEffect(SoundId soundId, Vector3 position, GameObject source, float volume)
    {
        if (!TryResolveEvent(soundId, SoundType.Effect, out EventReference eventReference))
            return false;
        _effects.RemoveAll(instance => !instance.isValid());
        try
        {
            var description = RuntimeManager.GetEventDescription(eventReference);
            if (description.isOneshot(out bool isOneShot) != FMOD.RESULT.OK || !isOneShot)
            {
                Debug.LogWarning("[SoundManager] Play and PlayAttached require a one-shot event: " + soundId);
                return false;
            }
            var instance = RuntimeManager.CreateInstance(eventReference);
            if (source != null)
                RuntimeManager.AttachInstanceToGameObject(instance, source);
            else
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            FMOD.RESULT result = instance.setVolume(Mathf.Max(0f, volume));
            if (result == FMOD.RESULT.OK)
                result = instance.start();
            if (result != FMOD.RESULT.OK)
            {
                RuntimeManager.DetachInstanceFromGameObject(instance);
                instance.release();
                Debug.LogError("[SoundManager] Failed to start effect playback: " + soundId + ", " + result);
                return false;
            }
            instance.release();
            _effects.Add(instance);
            return true;
        }
        catch (EventNotFoundException exception)
        {
            Debug.LogWarning("[SoundManager] Effect event not found in loaded banks: " + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// soundId의 등록과 expectedType을 확인하고 FMOD Bus를 준비한다.
    /// 유효하면 eventReference를 반환 인자로 채우고 true를 반환한다.
    /// </summary>
    private bool TryResolveEvent(SoundId soundId, SoundType expectedType, out EventReference eventReference)
    {
        eventReference = default;
        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog is not assigned.");
            return false;
        }
        if (!_catalog.TryGetEvent(soundId, out eventReference, out SoundType type))
            return false;
        if (type != expectedType)
        {
            Debug.LogWarning("[SoundManager] The playback API does not match the registered sound type: " + soundId + ", " + type);
            return false;
        }
        return TryInitializeBuses();
    }

    /// <summary>
    /// catalog의 BGM 및 효과음 Bus를 조회하고 보관한 음량을 적용한다.
    /// 연결을 확보하면 true를 반환하고 실패 시 Bus 오류를 보고한다.
    /// </summary>
    private bool TryInitializeBuses()
    {
        if (_busesReady && _bgmBus.isValid() && _effectBus.isValid())
            return true;
        if (_catalog == null)
            return false;
        try
        {
            _bgmBus = RuntimeManager.GetBus(_catalog.BgmBusPath);
            _effectBus = RuntimeManager.GetBus(_catalog.EffectBusPath);
            FMOD.RESULT bgmResult = _bgmBus.setVolume(_bgmVolume);
            FMOD.RESULT effectResult = _effectBus.setVolume(_effectVolume);
            _busesReady = bgmResult == FMOD.RESULT.OK && effectResult == FMOD.RESULT.OK;
            if (!_busesReady)
                Debug.LogError("[SoundManager] Failed to set bus volume: " + bgmResult + ", " + effectResult);
            return _busesReady;
        }
        catch (BusNotFoundException exception)
        {
            Debug.LogWarning("[SoundManager] Bus not found in loaded banks: " + exception.Message);
            return false;
        }
    }
}
