using System;
using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

public enum CombatSoundCue
{
    [InspectorName("일반 공격 1타")] FirstSlash,
    [InspectorName("일반 공격 2타")] SecondSlash,
    [InspectorName("일반 공격 3타")] ThirdSlash,
    [InspectorName("패링 반격 베기")] CounterSlash,
    [InspectorName("충전 발도 베기")] IaidoSlash,
    [InspectorName("시작 발도")] IntroUnsheath,
    [InspectorName("일반 공격 발도")] AttackUnsheath,
    [InspectorName("충전 공격 발도")] IaidoUnsheath,
    [InspectorName("패링 발도")] ParryUnsheath,
    [InspectorName("일반 접촉")] Contact,
    [InspectorName("가드 성공")] Guard,
    [InspectorName("패링 성공")] Parry,
    [InspectorName("플레이어 피격")] PlayerDamage
}

[CreateAssetMenu(fileName = "CombatSoundProfile", menuName = "Sound/Combat Sound Profile")]
public sealed class CombatSoundProfile : ScriptableObject
{
    public const string RESOURCE_PATH = "Sound/CombatSoundProfile";

    [Header("상황별 사운드")]
    [SerializeField] private CombatSoundEntry[] _entries = Array.Empty<CombatSoundEntry>();

    public IReadOnlyList<CombatSoundEntry> Entries => _entries;
}

[Serializable]
public sealed class CombatSoundEntry
{
    [Header("상황")]
    [SerializeField] private CombatSoundCue _cue;
    [SerializeField] private bool _enabled = true;

    [Header("음원")]
    [SerializeField] private SoundId _soundId;
    [SerializeField, Tooltip("지정하면 SoundLibrary의 ID 연결 대신 이 이벤트를 사용한다.")]
    private EventReference _eventOverride;
    [SerializeField, Min(0f)] private float _volume = 0.8f;
    [SerializeField, Range(0.25f, 4f)] private float _pitch = 1f;
    [SerializeField] private string _parameterName;
    [SerializeField] private string _label;

    [Header("시점")]
    [SerializeField, Tooltip("베기는 판정 구간 시작 기준으로 앞뒤 조정한다. 발도·접촉·피격은 사건 이후 지연만 적용한다.")]
    private float _timeOffset;

    public CombatSoundCue Cue => _cue;
    public bool Enabled => _enabled;
    public SoundId SoundId => _soundId;
    public EventReference EventOverride => _eventOverride;
    public float Volume => Mathf.Max(0f, _volume);
    public float Pitch => Mathf.Clamp(_pitch, 0.25f, 4f);
    public string ParameterName => _parameterName;
    public string Label => _label;
    public float TimeOffset => _timeOffset;
}
