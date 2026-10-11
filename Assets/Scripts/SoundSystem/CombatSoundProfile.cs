using System;
using System.Collections.Generic;

using UnityEngine;

using FMODUnity;

public enum CombatSoundCue
{
    [InspectorName("검 베기")] Slash = 0,
    [InspectorName("일반 접촉")] Contact = 9,
    [InspectorName("가드 성공")] Guard = 10,
    [InspectorName("패링 성공")] Parry = 11,
    [InspectorName("공통 피격 (플레이어·적)")] Damage = 12,
    [InspectorName("적 치명타격")] EnemyFinished = 13,
    [InspectorName("적 사망")] EnemyDie = 14,
    [InspectorName("플레이어 대시·회피")] Dodge = 15,
    [InspectorName("적 발견·접근 전조")] EnemyPrepare = 16
}

[CreateAssetMenu(fileName = "CombatSoundProfile", menuName = "Sound/Combat Sound Profile")]
public sealed class CombatSoundProfile : ScriptableObject
{
    public const string RESOURCE_PATH = "Sound/CombatSoundProfile";

    [Header("상황별 사운드")]
    [SerializeField] private CombatSoundEntry[] _entries = Array.Empty<CombatSoundEntry>();

    [Header("기본 BGM")]
    [SerializeField] private bool _playAmbience = true;
    [SerializeField] private SoundId _ambienceSound = SoundId.Ambience;
    [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.7f;
    [SerializeField] private bool _loopAmbience = true;

    public IReadOnlyList<CombatSoundEntry> Entries => _entries;
    public bool PlayAmbience => _playAmbience;
    public SoundId AmbienceSound => _ambienceSound;
    public float AmbienceVolume => _ambienceVolume;
    public bool LoopAmbience => _loopAmbience;
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

    [Header("시점")]
    [SerializeField, Tooltip("베기는 판정 구간 시작 기준으로 앞뒤 조정한다. 접촉·방어·공통 피격은 사건 이후 지연만 적용한다.")]
    private float _timeOffset;

    public CombatSoundCue Cue => _cue;
    public bool Enabled => _enabled;
    public SoundId SoundId => _soundId;
    public EventReference EventOverride => _eventOverride;
    public float Volume => Mathf.Max(0f, _volume);
    public float Pitch => Mathf.Clamp(_pitch, 0.25f, 4f);
    public float TimeOffset => _timeOffset;
}
