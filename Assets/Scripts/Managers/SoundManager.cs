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
    private GameObject _footstepOnlyOwner;

    [Header("Effects")]
    private readonly List<ActiveEffect> _effects = new List<ActiveEffect>();
    private float _combatFocusUntil;
#if UNITY_EDITOR
    private readonly List<string> _recentPlayback = new List<string>();
    public IReadOnlyList<string> RecentPlayback => _recentPlayback;
#endif

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

    /// <summary>owner 지정 시 플레이어 전투음·WarCry·이동 중 재생 예외를 유지한다. 다른 효과음을 정지하고 null이면 제한과 BGM 음소거를 해제한다.</summary>
    public void SetFootstepOnlyOwner(GameObject owner)
    {
        if (_footstepOnlyOwner == owner)
            return;
        _footstepOnlyOwner = owner;
        if (owner != null)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (CanPlayDuringMovement(_effects[i].Id, _effects[i].Owner, _effects[i].PlayDuringMovement))
                    continue;
                if (RuntimeManager.IsInitialized)
                    StopEffect(_effects[i].Instance, false);
                _effects.RemoveAt(i);
            }
        }
        if (_bgm.isValid() && RuntimeManager.IsInitialized)
            _bgm.setVolume(owner != null ? 0f : 1f);
    }

    /// <summary>
    /// soundId의 단발 효과음을 position에서 volume·pitch 배율로 재생한다.
    /// owner로 수명을 연결하고 playDuringMovement로 이동 중 재생 예외를 지정하며 성공 시 true를 반환한다.
    /// </summary>
    public bool Play(SoundId soundId, Vector3 position = default, float volume = 1f, GameObject owner = null, bool playDuringMovement = false, float pitch = 1f)
    {
        return PlayEffect(soundId, position, owner, false, volume, playDuringMovement, pitch: pitch);
    }

    /// <summary>
    /// soundId의 단발 효과음을 source에 연결하여 volume·pitch 배율로 재생한다.
    /// 발이 닿거나 공격하는 시점에 호출하며, 재생 중 source의 위치를 추적하고 성공하면 true를 반환한다.
    /// </summary>
    public bool PlayAttached(SoundId soundId, GameObject source, float volume = 1f, float pitch = 1f)
    {
        if (source == null)
        {
            Debug.LogWarning("[SoundManager] PlayAttached requires a source GameObject.");
            return false;
        }
        return PlayEffect(soundId, source.transform.position, source, true, volume, pitch: pitch);
    }

    /// <summary>
    /// soundId의 단발음을 source에 연결하고 parameterName의 Label을 재생 전에 지정한다.
    /// label과 volume·pitch를 FMOD에 전달하며 이벤트 설정과 재생이 성공하면 true를 반환한다.
    /// </summary>
    public bool PlayAttachedWithLabel(SoundId soundId, GameObject source, string parameterName, string label, float volume = 1f, float pitch = 1f)
    {
        if (source == null || string.IsNullOrWhiteSpace(parameterName) || string.IsNullOrWhiteSpace(label))
        {
            Debug.LogWarning("[SoundManager] A source, parameter name and label are required.");
            return false;
        }
        return PlayEffect(soundId, source.transform.position, source, true, volume, parameterName: parameterName, label: label, pitch: pitch);
    }

    /// <summary>
    /// entry의 중앙 설정을 source 또는 position에 적용해 효과음을 재생한다.
    /// 직접 이벤트·음량·pitch·Label을 사용하며 실제 재생 성공 여부를 반환한다.
    /// </summary>
    public bool PlayCombatCue(CombatSoundEntry entry, GameObject source, Vector3 position, bool attach)
    {
        if (!entry.Enabled || (attach && source == null)) return false;
        return PlayEffect(entry.SoundId, attach ? source.transform.position : position, source, attach, entry.Volume,
            parameterName: string.IsNullOrEmpty(entry.ParameterName) ? null : entry.ParameterName,
            label: entry.Label, pitch: entry.Pitch,
            eventOverride: entry.EventOverride.IsNull ? null : entry.EventOverride, cueName: entry.Cue.ToString());
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
            _bgm.setVolume(_footstepOnlyOwner != null ? 0f : 1f);
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
    /// owner가 재생한 soundId 효과음만 allowFadeOut 방식으로 정지한다.
    /// Library의 자연 종료 설정이면 단계 전환·취소에도 음원 끝을 유지하며 전체 정리 요청은 별도로 처리한다.
    /// </summary>
    public void StopFor(GameObject owner, SoundId soundId, bool allowFadeOut = false)
    {
        if (owner == null || _library.ShouldFinishNaturally(soundId)) return;
        foreach (ActiveEffect effect in _effects)
            if (effect.HasOwner && effect.Owner == owner && effect.Id == soundId)
                StopEffect(effect.Instance, allowFadeOut);
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
        _combatFocusUntil = 0f;
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
        _footstepOnlyOwner = null;
        StopBgm(false);
        StopEffects();
        _busesReady = false;
    }

    /// <summary>
    /// 보유 재생을 확인하여 비활성화 또는 파괴된 소유자의 사운드를 즉시 정지한다.
    /// 자연 종료된 핸들을 제거하고 전투 신호에 따른 발소리 음량·2D 패닝과 BGM 수명을 갱신한다.
    /// </summary>
    internal void Update()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _effects[i];
            if (effect.HasOwner && (effect.Owner == null || !effect.Owner.activeInHierarchy))
                StopEffect(effect.Instance, false);
            if (!RuntimeManager.IsInitialized || !effect.Instance.isValid())
            {
                _effects.RemoveAt(i);
                continue;
            }
            UpdateEffectMix(effect);
        }
        if (_hasBgmOwner && (_bgmOwner == null || !_bgmOwner.activeInHierarchy))
            StopBgm(false);
        else if (!_bgm.isValid() || !RuntimeManager.IsInitialized)
            StopBgm(false);
        else if (_bgm.getPlaybackState(out var state) == FMOD.RESULT.OK && state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
            StopBgm(false);
    }

    /// <summary>
    /// soundId의 단발 효과음을 position 또는 source에서 volume·pitch 배율로 재생한다.
    /// attach로 위치 추적과 playDuringMovement로 이동 예외를 선택하고 parameterName과 label을 시작 전에 적용한다.
    /// eventOverride와 cueName으로 중앙 설정의 음원 및 기록을 적용하며 재생 성공 여부를 반환한다.
    /// </summary>
    private bool PlayEffect(SoundId soundId, Vector3 position, GameObject source, bool attach, float volume, bool playDuringMovement = false, string parameterName = null, string label = null, float pitch = 1f,
        EventReference? eventOverride = null, string cueName = null)
    {
        EventReference eventReference;
        if (eventOverride.HasValue) eventReference = eventOverride.Value;
        else if (!TryResolveEvent(soundId, SoundType.Effect, out eventReference)) return false;
        _library.GetCueSettings(soundId, out bool combatCue, out bool spatialize2D);
        playDuringMovement |= combatCue;
        if (!CanPlayDuringMovement(soundId, source, playDuringMovement))
            return false;
        if (!ReferenceEquals(source, null) && (source == null || !source.activeInHierarchy))
        {
            Debug.LogWarning("[SoundManager] The sound owner must be active.");
            return false;
        }
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
            description.is3D(out bool is3D);
            description.getMinMaxDistance(out float minimumDistance, out float maximumDistance);
            if (attach)
                RuntimeManager.AttachInstanceToGameObject(instance, source);
            else
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            FMOD.RESULT result = instance.setVolume(Mathf.Max(0f, volume));
            if (result == FMOD.RESULT.OK)
                result = instance.setPitch(Mathf.Clamp(pitch, 0.25f, 4f));
            if (result == FMOD.RESULT.OK && parameterName != null)
                result = instance.setParameterByNameWithLabel(parameterName, label);
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
            if (combatCue)
                _combatFocusUntil = Time.unscaledTime + _library.CombatFocusDuration;
            var effect = new ActiveEffect(instance, source, soundId, playDuringMovement, position, attach,
                volume, spatialize2D && !is3D, minimumDistance, maximumDistance);
            _effects.Add(effect);
#if UNITY_EDITOR
            _recentPlayback.Add($"{Time.time:F2}s | {cueName ?? soundId.ToString()} | {eventReference.Path} | {source?.name ?? "world"} | {label} | volume={volume:F2}");
            if (_recentPlayback.Count > 16) _recentPlayback.RemoveAt(0);
#endif
            foreach (ActiveEffect active in _effects) UpdateEffectMix(active);
            return true;
        }
        catch (EventNotFoundException exception)
        {
            Debug.LogWarning("[SoundManager] Effect event not found in loaded banks: " + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// effect의 원래 음량에 전투 중 발소리 감쇠와 선택된 2D 공간 보정을 합성한다.
    /// FMOD 0번 Listener의 위치·방향과 이동 소유자를 사용하며 채널 생성 전 패닝은 다음 Update에서 재시도한다.
    /// </summary>
    private void UpdateEffectMix(ActiveEffect effect)
    {
        if (!effect.Instance.isValid()) return;
        float volume = Mathf.Max(0f, effect.Volume);
        if (Time.unscaledTime < _combatFocusUntil && (effect.Id == SoundId.Footstep || effect.Id == SoundId.EnemyFootstep))
            volume *= _library.CombatFootstepVolume;
        if (effect.Spatialize2D && StudioListener.ListenerCount > 0
            && RuntimeManager.StudioSystem.getListenerAttributes(0, out FMOD.ATTRIBUTES_3D listener) == FMOD.RESULT.OK)
        {
            Vector3 position = effect.Attach && effect.Owner != null ? effect.Owner.transform.position : effect.Position;
            Vector3 offset = position - new Vector3(listener.position.x, listener.position.y, listener.position.z);
            float distance = offset.magnitude;
            volume *= 1f - Mathf.InverseLerp(effect.MinimumDistance, Mathf.Max(effect.MinimumDistance + 0.01f, effect.MaximumDistance), distance);
            Vector3 right = Vector3.Cross(new Vector3(listener.up.x, listener.up.y, listener.up.z),
                new Vector3(listener.forward.x, listener.forward.y, listener.forward.z)).normalized;
            float pan = distance > 0.001f ? Mathf.Clamp(Vector3.Dot(offset / distance, right), -1f, 1f) : 0f;
            if (effect.Instance.getChannelGroup(out FMOD.ChannelGroup group) == FMOD.RESULT.OK)
                group.setPan(pan);
        }
        effect.Instance.setVolume(volume);
    }

    /// <summary>
    /// soundId와 source, playDuringMovement를 기존 이동 중 사운드 정책에 대조한다.
    /// 플레이어 발걸음과 전투음, 발도음 및 기존 전조 예외가 허용되면 true를 반환한다.
    /// </summary>
    private bool CanPlayDuringMovement(SoundId soundId, GameObject source, bool playDuringMovement)
    {
        return _footstepOnlyOwner == null || playDuringMovement || soundId == SoundId.WarCry
            || (source == _footstepOnlyOwner && (soundId == SoundId.Footstep || soundId == SoundId.SwordSwing
                || soundId == SoundId.SwordHit || soundId == SoundId.SwordUnsheath));
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
        private readonly SoundId _id;
        private readonly bool _playDuringMovement;
        private readonly Vector3 _position;
        private readonly bool _attach;
        private readonly float _volume;
        private readonly bool _spatialize2D;
        private readonly float _minimumDistance;
        private readonly float _maximumDistance;

        internal FMOD.Studio.EventInstance Instance => _instance;
        internal GameObject Owner => _owner;
        internal bool HasOwner => _hasOwner;
        internal SoundId Id => _id;
        internal bool PlayDuringMovement => _playDuringMovement;
        internal Vector3 Position => _position;
        internal bool Attach => _attach;
        internal float Volume => _volume;
        internal bool Spatialize2D => _spatialize2D;
        internal float MinimumDistance => _minimumDistance;
        internal float MaximumDistance => _maximumDistance;

        /// <summary>
        /// instance·owner·soundId·playDuringMovement와 위치·부착·음량·거리 입력을 보관한다.
        /// 공간 보정 플래그와 소유 여부를 기록하여 재생 수명 및 매 프레임 믹싱에 사용한다.
        /// </summary>
        internal ActiveEffect(FMOD.Studio.EventInstance instance, GameObject owner, SoundId soundId, bool playDuringMovement,
            Vector3 position, bool attach, float volume, bool spatialize2D, float minimumDistance, float maximumDistance)
        {
            _instance = instance;
            _owner = owner;
            _hasOwner = owner != null;
            _id = soundId;
            _playDuringMovement = playDuringMovement;
            _position = position;
            _attach = attach;
            _volume = volume;
            _spatialize2D = spatialize2D;
            _minimumDistance = minimumDistance;
            _maximumDistance = maximumDistance;
        }
    }
}
