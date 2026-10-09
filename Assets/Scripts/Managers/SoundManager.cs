using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

/// <summary>
/// 게임 코드의 SoundId 재생 요청을 FMOD에 전달하는 공통 사운드 서비스다.
/// 단발 효과음과 BGM의 수명 및 음량을 내부에서 관리하여 호출부에 FMOD 타입을 노출하지 않는다.
/// </summary>
public class SoundManager
{
    [Header("Configuration")]
    private readonly SoundLibrary _library;

    [Header("Effects")]
    private readonly List<ActiveEffect> _effects = new List<ActiveEffect>();

    [Header("BGM")]
    private FMOD.Studio.EventInstance _bgm;
    private SoundId _bgmId = SoundId.None;
    private bool _bgmReleaseRequested;
    private GameObject _bgmOwner;
    private bool _hasBgmOwner;

    [Header("Mixer")]
    private FMOD.Studio.Bus _bgmBus;
    private FMOD.Studio.Bus _effectBus;
    private bool _busesReady;
    private float _bgmVolume = 1f;
    private float _effectVolume = 1f;

    /// <summary>
    /// library의 사운드 ID와 FMOD 이벤트 연결을 보관한다.
    /// FMOD 재생 시스템은 실제 재생 요청 시 초기화하며, 매니저의 설정 상태를 준비한다.
    /// </summary>
    internal SoundManager(SoundLibrary library)
    {
        _library = library;
    }

    /// <summary>
    /// BGM Bus의 공통 음량 배율을 0~1 범위로 설정하거나 조회한다.
    /// 개별 음원 크기는 FMOD Studio에서 정하며 이 값은 옵션 음량에 사용한다.
    /// </summary>
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

    /// <summary>
    /// 효과음 Bus의 공통 음량 배율을 0~1 범위로 설정하거나 조회한다.
    /// 이미 재생 중인 효과음과 이후 재생할 효과음에 함께 적용한다.
    /// </summary>
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
    /// owner를 지정하면 해당 오브젝트의 수명을 따르며, 2D 이벤트는 위치를 사용하지 않고 성공 시 true를 반환한다.
    /// </summary>
    public bool Play(SoundId soundId, Vector3 position = default, float volume = 1f, GameObject owner = null)
    {
        return PlayEffect(soundId, position, owner, false, volume);
    }

    /// <summary>
    /// soundId의 단발 효과음을 source에 연결하여 volume 배율로 재생한다.
    /// 발이 닿거나 공격하는 시점에 호출하며, 재생 중 source의 위치를 추적하고 성공하면 true를 반환한다.
    /// </summary>
    public bool PlayAttached(SoundId soundId, GameObject source, float volume = 1f)
    {
        if (source == null)
        {
            Debug.LogWarning("[SoundManager] PlayAttached requires a source GameObject.");
            return false;
        }
        return PlayEffect(soundId, source.transform.position, source, true, volume);
    }

    /// <summary>
    /// soundId의 2D BGM을 재생하고 다른 곡이면 이전 BGM을 즉시 정지한다.
    /// 같은 BGM은 중복 생성하지 않고 owner에 소유권을 넘기며, 유효하면 true, 페이드 정지 중이면 false를 반환한다.
    /// </summary>
    public bool PlayBgm(SoundId soundId, GameObject owner = null)
    {
        if (!ReferenceEquals(owner, null) && (owner == null || !owner.activeInHierarchy))
            return false;
        if (!TryResolveEvent(soundId, SoundType.BGM, out EventReference eventReference))
            return false;
        if (_bgm.isValid() && _bgmId == soundId && _bgm.getPlaybackState(out var state) == FMOD.RESULT.OK && state != FMOD.Studio.PLAYBACK_STATE.STOPPED)
        {
            if (state == FMOD.Studio.PLAYBACK_STATE.STOPPING)
                return false;
            _bgmOwner = owner;
            _hasBgmOwner = owner != null;
            return true;
        }
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
            _bgmOwner = owner;
            _hasBgmOwner = owner != null;
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
        _bgmOwner = null;
        _hasBgmOwner = false;
    }

    /// <summary>
    /// owner의 효과음과 BGM만 allowFadeOut 설정으로 정지한다.
    /// 오브젝트 비활성화 등의 정리에 사용하며, 다른 소유자의 재생은 유지하고 null 요청은 무시한다.
    /// </summary>
    public void StopFor(GameObject owner, bool allowFadeOut = false)
    {
        if (owner == null)
            return;
        foreach (ActiveEffect effect in _effects)
        {
            if (effect.HasOwner && effect.Owner == owner)
                StopEffect(effect.Instance, allowFadeOut);
        }
        if (_hasBgmOwner && _bgmOwner == owner)
            StopBgm(allowFadeOut);
    }

    /// <summary>
    /// 외부 입력 없이 이 매니저의 단발 효과음을 모두 즉시 정지한다.
    /// BGM은 유지하며 보관한 효과음 목록을 비운다.
    /// </summary>
    public void StopEffects()
    {
        if (RuntimeManager.IsInitialized)
        {
            foreach (ActiveEffect effect in _effects)
                StopEffect(effect.Instance, false);
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
    /// 보유 재생을 확인하여 비활성화 또는 파괴된 소유자의 사운드를 즉시 정지한다.
    /// 외부 입력 없이 자연 종료된 핸들을 제거하고 BGM의 종료 및 소유자 상태를 정리한다.
    /// </summary>
    internal void Update()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _effects[i];
            if (effect.HasOwner && (effect.Owner == null || !effect.Owner.activeInHierarchy))
                StopEffect(effect.Instance, false);
            if (!RuntimeManager.IsInitialized || !effect.Instance.isValid())
                _effects.RemoveAt(i);
        }
        if (_hasBgmOwner && (_bgmOwner == null || !_bgmOwner.activeInHierarchy))
            StopBgm(false);
        else if (!_bgm.isValid() || !RuntimeManager.IsInitialized)
            StopBgm(false);
        else if (_bgm.getPlaybackState(out var state) == FMOD.RESULT.OK && state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
            StopBgm(false);
    }

    /// <summary>
    /// soundId의 단발 효과음을 position 또는 source에서 volume 배율로 재생한다.
    /// attach로 위치 추적을 선택하고 해제를 요청한 인스턴스와 소유자만 내부에 보관하며 성공하면 true를 반환한다.
    /// </summary>
    private bool PlayEffect(SoundId soundId, Vector3 position, GameObject source, bool attach, float volume)
    {
        if (!ReferenceEquals(source, null) && (source == null || !source.activeInHierarchy))
        {
            Debug.LogWarning("[SoundManager] The sound owner must be active.");
            return false;
        }
        if (!TryResolveEvent(soundId, SoundType.Effect, out EventReference eventReference))
            return false;
        Update();
        try
        {
            var description = RuntimeManager.GetEventDescription(eventReference);
            if (description.isOneshot(out bool isOneShot) != FMOD.RESULT.OK || !isOneShot)
            {
                Debug.LogWarning("[SoundManager] Play and PlayAttached require a one-shot event: " + soundId);
                return false;
            }
            var instance = RuntimeManager.CreateInstance(eventReference);
            if (attach)
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
            _effects.Add(new ActiveEffect(instance, source));
            return true;
        }
        catch (EventNotFoundException exception)
        {
            Debug.LogWarning("[SoundManager] Effect event not found in loaded banks: " + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// instance를 allowFadeOut 설정에 따라 페이드 또는 즉시 정지한다.
    /// 단발음은 시작 시 해제를 요청했으므로 즉시 정지할 때 위치 추적만 추가로 해제한다.
    /// </summary>
    private static void StopEffect(FMOD.Studio.EventInstance instance, bool allowFadeOut)
    {
        if (!RuntimeManager.IsInitialized || !instance.isValid())
            return;
        instance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        if (!allowFadeOut)
            RuntimeManager.DetachInstanceFromGameObject(instance);
    }

    /// <summary>
    /// soundId의 등록과 expectedType을 확인하고 FMOD Bus를 준비한다.
    /// 유효하면 eventReference를 반환 인자로 채우고 true를 반환한다.
    /// </summary>
    private bool TryResolveEvent(SoundId soundId, SoundType expectedType, out EventReference eventReference)
    {
        eventReference = default;
        if (_library == null)
        {
            Debug.LogWarning("[SoundManager] SoundLibrary is not assigned.");
            return false;
        }
        if (!_library.TryGetEvent(soundId, out eventReference, out SoundType type))
            return false;
        if (type != expectedType)
        {
            Debug.LogWarning("[SoundManager] The playback API does not match the registered sound type: " + soundId + ", " + type);
            return false;
        }
        return TryInitializeBuses();
    }

    /// <summary>
    /// library의 BGM 및 효과음 Bus를 조회하고 보관한 음량을 적용한다.
    /// 연결을 확보하면 true를 반환하고 실패 시 Bus 오류를 보고한다.
    /// </summary>
    private bool TryInitializeBuses()
    {
        if (_busesReady && _bgmBus.isValid() && _effectBus.isValid())
            return true;
        if (_library == null)
            return false;
        try
        {
            _bgmBus = RuntimeManager.GetBus(_library.BgmBusPath);
            _effectBus = RuntimeManager.GetBus(_library.EffectBusPath);
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

    private readonly struct ActiveEffect
    {
        [Header("Effect")]
        private readonly FMOD.Studio.EventInstance _instance;
        private readonly GameObject _owner;
        private readonly bool _hasOwner;

        internal FMOD.Studio.EventInstance Instance => _instance;
        internal GameObject Owner => _owner;
        internal bool HasOwner => _hasOwner;

        /// <summary>
        /// 재생한 instance와 owner를 내부 정리용 데이터로 기록한다.
        /// 소유자 지정 여부를 보관하여 파괴된 Unity 오브젝트와 소유자 없는 재생을 구분한다.
        /// </summary>
        internal ActiveEffect(FMOD.Studio.EventInstance instance, GameObject owner)
        {
            _instance = instance;
            _owner = owner;
            _hasOwner = owner != null;
        }
    }
}
