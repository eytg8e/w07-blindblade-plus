using System;
using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

[CreateAssetMenu(fileName = "SoundCatalog", menuName = "Sound/Sound Catalog")]
public class SoundCatalog : ScriptableObject
{
    [Header("Events")]
    [SerializeField] private SoundEntry[] _sounds = Array.Empty<SoundEntry>();

    [Header("Mixer")]
    [SerializeField] private string _bgmBusPath = "bus:/BGM_Bus";
    [SerializeField] private string _effectBusPath = "bus:/SFX_Bus";

    internal string BgmBusPath => _bgmBusPath;
    internal string EffectBusPath => _effectBusPath;

    /// <summary>
    /// soundId에 연결된 FMOD 이벤트와 사운드 종류를 조회한다.
    /// 등록이 유효하면 eventReference와 soundType을 채우고 true를 반환하며, 누락 또는 중복이면 false를 반환한다.
    /// </summary>
    internal bool TryGetEvent(SoundId soundId, out EventReference eventReference, out SoundType soundType)
    {
        eventReference = default;
        soundType = SoundType.Effect;
        SoundEntry match = null;
        foreach (SoundEntry entry in _sounds)
        {
            if (entry.Id != soundId)
                continue;
            if (match != null)
            {
                Debug.LogError("[SoundManager] Duplicate SoundId: " + soundId, this);
                return false;
            }
            match = entry;
        }
        if (soundId == SoundId.None || match == null || match.Event.IsNull)
        {
            Debug.LogWarning("[SoundManager] No event registered for SoundId: " + soundId, this);
            return false;
        }
        eventReference = match.Event;
        soundType = match.Type;
        return true;
    }

    void OnValidate()
    {
        var ids = new HashSet<SoundId>();
        foreach (SoundEntry entry in _sounds)
        {
            if (entry.Id == SoundId.None || !ids.Add(entry.Id))
                Debug.LogWarning("[SoundManager] SoundId must not be None or duplicated: " + entry.Id, this);
        }
    }

    [Serializable]
    private class SoundEntry
    {
        [Header("Sound")]
        [SerializeField] private SoundId _id;
        [SerializeField] private SoundType _type = SoundType.Effect;
        [SerializeField] private EventReference _event;

        internal SoundId Id => _id;
        internal SoundType Type => _type;
        internal EventReference Event => _event;
    }
}
