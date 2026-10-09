using System;
using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 효과 ID와 프리팹 및 초기 풀 개수를 보관한다.
/// </summary>
[CreateAssetMenu(fileName = "EffectLibrary", menuName = "Effects/Effect Library")]
public sealed class EffectLibrary : ScriptableObject
{
    [Header("효과 등록")]
    [SerializeField] private EffectEntry[] _effects = Array.Empty<EffectEntry>();

    void OnValidate()
    {
        if (_effects.Length > 0)
            ValidateEntries();
    }

    /// <summary>
    /// 등록된 효과 설정을 검증하고 기존 공통 풀을 초기 개수만큼 준비한다.
    /// Library의 프리팹과 초기 개수를 사용하며 성공 여부를 반환한다.
    /// </summary>
    internal bool PreparePools()
    {
        if (!ValidateEntries())
            return false;
        foreach (EffectEntry entry in _effects)
            Managers.Pool.CreatePool(entry.Prefab, entry.PrewarmCount);
        return true;
    }

    /// <summary>
    /// effectId에 등록된 프리팹을 찾아 prefab에 저장한다.
    /// 유효한 단일 등록이면 true, 누락·중복이면 원인을 기록하고 false를 반환한다.
    /// </summary>
    internal bool TryGetPrefab(EffectId effectId, out GameObject prefab)
    {
        prefab = null;
        EffectEntry match = null;
        foreach (EffectEntry entry in _effects)
        {
            if (entry.Id != effectId)
                continue;
            if (match != null)
            {
                Debug.LogError("[EffectLibrary] Duplicate EffectId: " + effectId, this);
                return false;
            }
            match = entry;
        }
        if (effectId == EffectId.None || match == null || match.Prefab == null)
        {
            Debug.LogWarning("[EffectLibrary] No prefab registered for EffectId: " + effectId, this);
            return false;
        }
        prefab = match.Prefab;
        return true;
    }

    /// <summary>
    /// 등록된 ID·프리팹과 루트 Particle System 또는 검 궤적 연결을 검증한다.
    /// Library 설정에 누락·중복·종류 불일치가 없을 때 true를 반환한다.
    /// </summary>
    private bool ValidateEntries()
    {
        if (_effects.Length == 0)
        {
            Debug.LogError("[EffectLibrary] No effects are registered.", this);
            return false;
        }
        HashSet<EffectId> ids = new HashSet<EffectId>();
        foreach (EffectEntry entry in _effects)
        {
            if (entry.Id == EffectId.None || !ids.Add(entry.Id))
            {
                Debug.LogError("[EffectLibrary] EffectId must not be None or duplicated: " + entry.Id, this);
                return false;
            }
            if (entry.Prefab == null)
            {
                Debug.LogError("[EffectLibrary] Prefab is not assigned for EffectId: " + entry.Id, this);
                return false;
            }
            bool isTrail = entry.Prefab.GetComponent<SwordSlashTrail>() != null;
            bool isBurst = entry.Prefab.GetComponent<ParticleSystem>() != null;
            if (isTrail == isBurst || (entry.Id == EffectId.SwordSlashTrail) != isTrail)
            {
                Debug.LogError("[EffectLibrary] Effect component does not match the ID: " + entry.Id, this);
                return false;
            }
            foreach (ParticleSystem system in entry.Prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.main.loop || system.main.playOnAwake || system.main.stopAction != ParticleSystemStopAction.None)
                {
                    Debug.LogError("[EffectLibrary] Set Loop off, Play On Awake off and Stop Action None: "
                        + entry.Id + "/" + system.name, this);
                    return false;
                }
            }
        }
        return true;
    }

    [Serializable]
    private sealed class EffectEntry
    {
        [Header("효과")]
        [SerializeField] private EffectId _id;
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0)] private int _prewarmCount = 3;
        public EffectId Id => _id;
        public GameObject Prefab => _prefab;
        public int PrewarmCount => _prewarmCount;
    }
}
