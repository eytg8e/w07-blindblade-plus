using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

using FMODUnity;

public sealed class SoundControlWindow : EditorWindow
{
    private const string PROFILE_PATH = "Assets/Resources/Sound/CombatSoundProfile.asset";
    private const string LIBRARY_PATH = "Assets/Resources/Sound/SoundLibrary.asset";
    private const string STYLE_PATH = "Assets/Editor/SoundControlWindow.uss";

    [Header("설정")]
    private CombatSoundProfile _profile;
    private SoundLibrary _library;
    private SerializedObject _profileObject;
    private SerializedObject _libraryObject;

    [Header("미리 듣기·기록")]
    private FMOD.Studio.EventInstance _preview;
    private Label _status;
    private Label _records;
    private string _lastRecord;
    private IVisualElementScheduledItem _recordUpdate;

    /// <summary>
    /// 메뉴에서 중앙 사운드 편집 창을 열고 기본 제목을 설정한다.
    /// 외부 입력 없이 기존 설정 에셋을 편집하는 창을 반환받아 표시한다.
    /// </summary>
    [MenuItem("BlindBlade/사운드 편집")]
    public static void Open()
    {
        GetWindow<SoundControlWindow>("사운드 편집");
    }

    void OnDisable()
    {
        _recordUpdate?.Pause();
        StopPreview();
    }

    void CreateGUI()
    {
        _recordUpdate?.Pause();
        rootVisualElement.Clear();
        rootVisualElement.AddToClassList("sound-control");
        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);
        if (style != null) rootVisualElement.styleSheets.Add(style);
        _profile = AssetDatabase.LoadAssetAtPath<CombatSoundProfile>(PROFILE_PATH);
        _library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LIBRARY_PATH);
        if (_profile == null || _library == null)
        {
            rootVisualElement.Add(new HelpBox("CombatSoundProfile 또는 SoundLibrary 설정 에셋이 없습니다.", HelpBoxMessageType.Error));
            return;
        }
        var toolbar = new VisualElement();
        toolbar.AddToClassList("toolbar");
        toolbar.Add(new Button(SaveSettings) { text = "설정 저장" });
        toolbar.Add(new Button(StopPreview) { text = "미리 듣기 정지" });
        toolbar.Add(new Button(AddEntry) { text = "상황에 음원 추가" });
        rootVisualElement.Add(toolbar);
        rootVisualElement.Add(new HelpBox("베기는 FMOD에서 랜덤 음원을 선택합니다. 발도음은 재생하지 않습니다. 베기 시간 보정은 판정 구간 시작 기준이며 접촉·방어·공통 피격은 사건 이후 지연입니다. 같은 상황의 여러 항목은 함께 재생됩니다.", HelpBoxMessageType.Info));
        _status = new Label();
        _status.AddToClassList("status");
        rootVisualElement.Add(_status);
        var scroll = new ScrollView();
        scroll.AddToClassList("settings");
        rootVisualElement.Add(scroll);
        _profileObject = new SerializedObject(_profile);
        SerializedProperty entries = _profileObject.FindProperty("_entries");
        var cues = new VisualElement();
        for (int i = 0; i < entries.arraySize; i++)
            cues.Add(CreateEntry(entries.GetArrayElementAtIndex(i), i));
        cues.Bind(_profileObject);
        scroll.Add(cues);
        var ambience = new Foldout { text = "기본 BGM", value = false };
        ambience.Add(new PropertyField(_profileObject.FindProperty("_playAmbience"), "세션 시작 시 재생"));
        ambience.Add(new PropertyField(_profileObject.FindProperty("_ambienceSound"), "BGM SoundId"));
        ambience.Add(new PropertyField(_profileObject.FindProperty("_ambienceVolume"), "음량"));
        ambience.Add(new PropertyField(_profileObject.FindProperty("_loopAmbience"), "반복 재생"));
        ambience.Add(new HelpBox("FMOD Loop Region이 있으면 이벤트 자체가 반복합니다. 단발 이벤트는 종료 후 재시작하므로 짧은 간격이 생길 수 있습니다. Play 중 적용 버튼으로 설정을 반영할 수 있습니다.", HelpBoxMessageType.Info));
        ambience.Add(new Button(ApplyBgm) { text = "Play 중 BGM 적용 / 재생" });
        ambience.Bind(_profileObject);
        scroll.Add(ambience);
        _libraryObject = new SerializedObject(_library);
        SerializedProperty sounds = _libraryObject.FindProperty("_sounds");
        var enemySounds = new Foldout { text = "적 공격·피격·보스 BGM", value = true };
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty sound = sounds.GetArrayElementAtIndex(i);
            int id = sound.FindPropertyRelative("_id").intValue;
            if (id == (int)SoundId.Footstep || id == (int)SoundId.EnemyFootstep)
            {
                VisualElement footstep = CreateFootstep(sound, i, id == (int)SoundId.Footstep);
                footstep.Bind(_libraryObject);
                scroll.Add(footstep);
            }
            else if (IsPatternSound((SoundId)id))
                enemySounds.Add(CreatePatternSound(sounds.GetArrayElementAtIndex(i), i));
        }
        enemySounds.Bind(_libraryObject);
        scroll.Add(enemySounds);
        var library = new Foldout { text = "공통 음원 연결·적 베기·발소리 감쇠", value = false };
        library.Add(new HelpBox("적 베기의 Finish Naturally를 켜면 개별 정지 요청에도 음원 끝을 유지합니다. 공격 취소에도 꼬리가 남으며, 소유자 비활성화와 전체 정지는 유지됩니다.", HelpBoxMessageType.Info));
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty sound = sounds.GetArrayElementAtIndex(i);
            int id = sound.FindPropertyRelative("_id").intValue;
            if (id == 1 || id == 5 || id == 6 || id == 7 || id == 8 || id == 12 || IsPatternSound((SoundId)id)) continue;
            library.Add(new PropertyField(sound, ((SoundId)id).ToString()));
        }
        library.Add(new HelpBox("전투 상태를 검사하지 않습니다. Combat Cue 소리가 날 때마다 아래 시간 동안 플레이어·적 발소리를 감쇠합니다. 음량 배율 1이면 감쇠하지 않습니다.", HelpBoxMessageType.Info));
        library.Add(new PropertyField(_libraryObject.FindProperty("_combatFocusDuration"), "전투음 직후 발소리 감쇠 시간"));
        library.Add(new PropertyField(_libraryObject.FindProperty("_combatFootstepVolume"), "감쇠 중 발소리 음량 배율"));
        library.Bind(_libraryObject);
        scroll.Add(library);
        _records = new Label();
        _records.AddToClassList("records");
        var recordSection = new Foldout { text = "최근 실제 재생 기록", value = true };
        recordSection.Add(_records);
        scroll.Add(recordSection);
        _lastRecord = null;
        _recordUpdate = rootVisualElement.schedule.Execute(UpdateRecords).Every(250);
    }

    /// <summary>
    /// entry의 상황·음원·시간 필드를 하나의 접힌 편집 영역으로 만든다.
    /// index로 미리 듣기와 항목 삭제 대상을 연결하여 VisualElement를 반환한다.
    /// </summary>
    private VisualElement CreateEntry(SerializedProperty entry, int index)
    {
        SerializedProperty cue = entry.FindPropertyRelative("_cue");
        string cueTitle = cue.enumValueIndex >= 0 ? cue.enumDisplayNames[cue.enumValueIndex] : "이전 상황";
        var foldout = new Foldout { text = cueTitle + " [" + (index + 1) + "]", value = false };
        foldout.AddToClassList("cue");
        foldout.Add(new PropertyField(cue, "상황"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_enabled"), "재생 사용"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_soundId"), "기본 SoundId"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_eventOverride"), "직접 FMOD 이벤트 (선택)"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_volume"), "음량"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_pitch"), "음높이"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_timeOffset"), "시간 보정 / 지연 (초)"));
        var buttons = new VisualElement();
        buttons.AddToClassList("toolbar");
        buttons.Add(new Button(() => Preview(index)) { text = "이 음원 미리 듣기" });
        buttons.Add(new Button(() => RemoveEntry(index)) { text = "항목 삭제" });
        foldout.Add(buttons);
        return foldout;
    }

    /// <summary>
    /// sound의 플레이어·적 발소리 음원과 독립 재생 배율을 편집하는 영역을 만든다.
    /// index로 미리 듣기를 연결하고 실제 발접촉 시점이 유지됨을 표시한 VisualElement를 반환한다.
    /// </summary>
    private VisualElement CreateFootstep(SerializedProperty sound, int index, bool player)
    {
        var foldout = new Foldout { text = player ? "플레이어 발소리" : "적 발소리", value = false };
        foldout.AddToClassList("cue");
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_enabled"), "재생 사용"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_event"), "FMOD 이벤트"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_volumeMultiplier"), "음량 배율"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_pitchMultiplier"), "음높이 배율"));
        foldout.Add(new HelpBox("실제 발접촉 사건에 재생합니다. 음량 배율은 기존 보행·은신·적 발걸음 음량에 곱하며, 미리 듣기는 배율만 적용합니다.", HelpBoxMessageType.Info));
        foldout.Add(new Button(() => PreviewFootstep(index)) { text = "발소리 미리 듣기" });
        return foldout;
    }

    /// <summary>
    /// soundId가 새 적 음성·심장박동 또는 보스 BGM이면 true를 반환한다.
    /// 전용 편집 영역에 표시할 등록 항목을 선택하며 기존 공통 음원은 제외한다.
    /// </summary>
    private static bool IsPatternSound(SoundId soundId)
    {
        return soundId == SoundId.BossBgm || soundId == SoundId.EnemyDamaged
            || soundId == SoundId.EnemyGetParryed || soundId == SoundId.EnemyHeartbeat
            || soundId == SoundId.EnemyAttackVoice || soundId == SoundId.EnemyRangedAttackVoice;
    }

    /// <summary>
    /// sound의 이벤트·사용 여부·음량·pitch·정지 정책을 독립 편집하는 영역을 반환한다.
    /// index로 미리 듣기를 연결하고 효과음의 이동 중 재생·발소리 감쇠 설정을 노출한다.
    /// </summary>
    private VisualElement CreatePatternSound(SerializedProperty sound, int index)
    {
        SoundId soundId = (SoundId)sound.FindPropertyRelative("_id").intValue;
        bool bgm = soundId == SoundId.BossBgm;
        var foldout = new Foldout { text = bgm ? "BossBGM" : soundId.ToString(), value = false };
        foldout.AddToClassList("cue");
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_enabled"), "재생 사용"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_event"), "FMOD 이벤트"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_volumeMultiplier"), "음량 배율"));
        foldout.Add(new PropertyField(sound.FindPropertyRelative("_pitchMultiplier"), "음높이 배율"));
        if (!bgm)
        {
            foldout.Add(new PropertyField(sound.FindPropertyRelative("_combatCue"), "전투 신호·이동 중 재생 허용"));
            foldout.Add(new PropertyField(sound.FindPropertyRelative("_finishNaturally"), "일반 정지 요청에서 끝까지 재생"));
            foldout.Add(new HelpBox("공격 취소의 강제 정지에서는 자연 종료 설정과 관계없이 정리합니다. 심장박동 음높이는 1로 유지하는 것을 권장합니다.", HelpBoxMessageType.Info));
        }
        else
            foldout.Add(new HelpBox("BGM 미리 듣기는 정지 버튼 또는 창 닫기로 종료합니다. 게임의 보스방 진입과 퇴장 사건은 발생하지 않습니다.", HelpBoxMessageType.Info));
        foldout.Add(new Button(() => PreviewPatternSound(index)) { text = "이 음원 미리 듣기" });
        return foldout;
    }

    /// <summary>
    /// index의 중앙 등록 이벤트와 음량·pitch 배율로 미리 듣기를 요청한다.
    /// 보스 BGM은 반복 이벤트도 허용하며 재생 사용을 끈 항목은 시작하지 않는다.
    /// </summary>
    private void PreviewPatternSound(int index)
    {
        _libraryObject.Update();
        SerializedProperty sound = _libraryObject.FindProperty("_sounds").GetArrayElementAtIndex(index);
        if (!sound.FindPropertyRelative("_enabled").boolValue)
        {
            StopPreview();
            _status.text = "재생 사용이 꺼진 음원입니다.";
            return;
        }
        PreviewReference(ReadReference(sound.FindPropertyRelative("_event")),
            sound.FindPropertyRelative("_volumeMultiplier").floatValue,
            sound.FindPropertyRelative("_pitchMultiplier").floatValue,
            sound.FindPropertyRelative("_id").intValue == (int)SoundId.BossBgm);
    }

    /// <summary>
    /// index의 발소리 이벤트와 해당 역할의 음량·pitch 배율로 미리 듣기를 실행한다.
    /// 플레이어·적의 실제 걸음 사건이나 다른 발소리 설정은 변경하지 않는다.
    /// </summary>
    private void PreviewFootstep(int index)
    {
        _libraryObject.Update();
        SerializedProperty sound = _libraryObject.FindProperty("_sounds").GetArrayElementAtIndex(index);
        EventReference reference = ReadReference(sound.FindPropertyRelative("_event"));
        PreviewReference(reference, sound.FindPropertyRelative("_volumeMultiplier").floatValue,
            sound.FindPropertyRelative("_pitchMultiplier").floatValue);
    }

    /// <summary>
    /// 현재 프로필에 기본 상황 항목을 추가하고 편집 화면을 갱신한다.
    /// SerializedObject로 Undo를 지원하며 새 항목은 음원을 지정하기 전까지 비활성화한다.
    /// </summary>
    private void AddEntry()
    {
        _profileObject.Update();
        SerializedProperty entries = _profileObject.FindProperty("_entries");
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_enabled").boolValue = false;
        _profileObject.ApplyModifiedProperties();
        CreateGUI();
    }

    /// <summary>
    /// index의 상황 음원 항목을 프로필에서 제거하고 편집 화면을 갱신한다.
    /// SerializedObject로 Undo를 지원하며 항목이 없는 상황은 재생하지 않는다.
    /// </summary>
    private void RemoveEntry(int index)
    {
        _profileObject.Update();
        _profileObject.FindProperty("_entries").DeleteArrayElementAtIndex(index);
        _profileObject.ApplyModifiedProperties();
        CreateGUI();
    }

    /// <summary>
    /// 이 창의 프로필과 Library 변경만 디스크에 저장한다.
    /// 다른 에셋이나 씬의 수정은 저장하지 않으며 결과를 상태 표시줄에 기록한다.
    /// </summary>
    private void SaveSettings()
    {
        AssetDatabase.SaveAssetIfDirty(_profile);
        AssetDatabase.SaveAssetIfDirty(_library);
        _status.text = "사운드 설정을 저장했습니다.";
    }

    /// <summary>
    /// Play 중 기본 BGM 프로필의 재생 사용·음량·반복 설정을 현재 플레이어에 적용한다.
    /// 사용 해제 시 음악을 정지하고 편집 모드에서는 상태 표시줄에 안내만 기록한다.
    /// </summary>
    private void ApplyBgm()
    {
        if (!EditorApplication.isPlaying || Game.Player == null)
        {
            _status.text = "게임 Play 중에 BGM 설정을 적용할 수 있습니다.";
            return;
        }
        if (!_profile.PlayAmbience) Managers.Sound.StopBgm();
        else Managers.Sound.PlayBgm(_profile.AmbienceSound, Game.Player.gameObject, _profile.LoopAmbience, _profile.AmbienceVolume);
        _status.text = "BGM 설정을 적용했습니다.";
    }

    /// <summary>
    /// index의 이벤트를 FMOD Editor 시스템에서 음량·pitch·Label을 적용해 미리 재생한다.
    /// 실제 게임 사건은 발생시키지 않으며 실패 시 인스턴스를 해제하고 오류를 표시한다.
    /// </summary>
    private void Preview(int index)
    {
        CombatSoundEntry entry = _profile.Entries[index];
        EventReference reference = entry.EventOverride;
        if (reference.IsNull)
        {
            var serialized = new SerializedObject(_library);
            SerializedProperty sounds = serialized.FindProperty("_sounds");
            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty sound = sounds.GetArrayElementAtIndex(i);
                if (sound.FindPropertyRelative("_id").intValue != (int)entry.SoundId) continue;
                var eventField = sound.FindPropertyRelative("_event");
                reference = ReadReference(eventField);
                break;
            }
        }
        PreviewReference(reference, entry.Volume, entry.Pitch);
    }

    /// <summary>
    /// property에 저장된 FMOD Path와 GUID를 EventReference로 읽어 반환한다.
    /// 이전 Event Browser 캐시가 남아 있어도 에셋에 지정한 이벤트를 그대로 사용한다.
    /// </summary>
    private static EventReference ReadReference(SerializedProperty property)
    {
        SerializedProperty guid = property.FindPropertyRelative("Guid");
        return new EventReference
        {
            Path = property.FindPropertyRelative("Path").stringValue,
            Guid = new FMOD.GUID
            {
                Data1 = guid.FindPropertyRelative("Data1").intValue,
                Data2 = guid.FindPropertyRelative("Data2").intValue,
                Data3 = guid.FindPropertyRelative("Data3").intValue,
                Data4 = guid.FindPropertyRelative("Data4").intValue
            }
        };
    }

    /// <summary>
    /// reference·volume·pitch를 FMOD Editor 시스템에 적용해 미리 재생한다.
    /// allowLoop가 true면 반복 이벤트도 허용하고 이전 인스턴스를 정리하며 결과를 상태 표시줄에 기록한다.
    /// </summary>
    private void PreviewReference(EventReference reference, float volume, float pitch, bool allowLoop = false)
    {
        StopPreview();
        if (reference.IsNull) { _status.text = "재생할 FMOD 이벤트를 지정해 주세요."; return; }
        EditorUtils.LoadPreviewBanks();
        FMOD.RESULT result = EditorUtils.System.getEventByID(reference.Guid, out FMOD.Studio.EventDescription description);
        if (result != FMOD.RESULT.OK) { _status.text = "이벤트 조회 실패: " + result; return; }
        result = description.isOneshot(out bool isOneShot);
        if (result != FMOD.RESULT.OK) { _status.text = "이벤트 속성 조회 실패: " + result; return; }
        if (!isOneShot && !allowLoop) { _status.text = "단발 이벤트만 미리 듣기에 사용할 수 있습니다."; return; }
        if (result == FMOD.RESULT.OK) result = description.createInstance(out _preview);
        if (result == FMOD.RESULT.OK) result = _preview.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));
        if (result == FMOD.RESULT.OK) result = _preview.setVolume(Mathf.Max(0f, volume));
        if (result == FMOD.RESULT.OK) result = _preview.setPitch(Mathf.Clamp(pitch, 0.25f, 4f));
        if (result == FMOD.RESULT.OK) result = _preview.start();
        _status.text = result == FMOD.RESULT.OK ? "미리 듣기: " + reference.Path : "재생 실패: " + result;
        if (result != FMOD.RESULT.OK) StopPreview();
    }

    /// <summary>
    /// 창이 소유한 미리 듣기 인스턴스만 즉시 정지하고 해제한다.
    /// 외부 입력 없이 게임의 SoundManager 재생은 유지한다.
    /// </summary>
    private void StopPreview()
    {
        if (!_preview.isValid()) return;
        _preview.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _preview.release();
        _preview.clearHandle();
    }

    /// <summary>
    /// 플레이 중 매니저의 최근 재생 기록을 표시하고 종료한 미리 듣기 핸들을 해제한다.
    /// 새 기록이 없으면 표시를 유지하며 게임 재생과 상태는 변경하지 않는다.
    /// </summary>
    private void UpdateRecords()
    {
        if (_preview.isValid() && _preview.getPlaybackState(out var state) == FMOD.RESULT.OK
            && state == FMOD.Studio.PLAYBACK_STATE.STOPPED) StopPreview();
        if (!EditorApplication.isPlaying || !Managers.IsInitialized)
        {
            _records.text = "Play 중 실제 재생 기록이 표시됩니다.";
            _lastRecord = null;
            return;
        }
        var records = Managers.Sound.RecentPlayback;
        if (records.Count == 0) return;
        string latest = records[records.Count - 1];
        if (latest == _lastRecord) return;
        _lastRecord = latest;
        _records.text = string.Join("\n", records);
    }
}
