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
        rootVisualElement.Add(new HelpBox("변경은 다음 행동부터 적용됩니다. 베기 시간 보정은 판정 구간 시작 기준, 발도·접촉·피격은 사건 이후 지연입니다. 같은 상황에 여러 항목을 추가하면 함께 재생됩니다. 미리 듣기는 타이밍·게임 믹싱을 재현하지 않습니다.", HelpBoxMessageType.Info));
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
        _libraryObject = new SerializedObject(_library);
        var library = new Foldout { text = "공통 음원·적 사운드·발소리·믹싱", value = false };
        library.Add(new HelpBox("적 준비음·스윙음의 Finish Naturally를 켜면 개별 정지 요청에도 음원 끝을 유지합니다. 공격 취소에도 꼬리가 남으며, 소유자 비활성화와 전체 정지는 유지됩니다.", HelpBoxMessageType.Info));
        library.Add(new PropertyField(_libraryObject.FindProperty("_sounds"), "SoundId와 FMOD 이벤트"));
        library.Add(new PropertyField(_libraryObject.FindProperty("_combatFocusDuration"), "전투음 집중 시간"));
        library.Add(new PropertyField(_libraryObject.FindProperty("_combatFootstepVolume"), "전투 중 발소리 배율"));
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
        var foldout = new Foldout { text = cue.enumDisplayNames[cue.enumValueIndex] + " [" + (index + 1) + "]", value = false };
        foldout.AddToClassList("cue");
        foldout.Add(new PropertyField(cue, "상황"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_enabled"), "재생 사용"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_soundId"), "기본 SoundId"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_eventOverride"), "직접 FMOD 이벤트 (선택)"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_volume"), "음량"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_pitch"), "음높이"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_parameterName"), "FMOD 파라미터 이름"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_label"), "파라미터 Label"));
        foldout.Add(new PropertyField(entry.FindPropertyRelative("_timeOffset"), "시간 보정 / 지연 (초)"));
        var buttons = new VisualElement();
        buttons.AddToClassList("toolbar");
        buttons.Add(new Button(() => Preview(index)) { text = "이 음원 미리 듣기" });
        buttons.Add(new Button(() => RemoveEntry(index)) { text = "항목 삭제" });
        foldout.Add(buttons);
        return foldout;
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
    /// index의 이벤트를 FMOD Editor 시스템에서 음량·pitch·Label을 적용해 미리 재생한다.
    /// 실제 게임 사건은 발생시키지 않으며 실패 시 인스턴스를 해제하고 오류를 표시한다.
    /// </summary>
    private void Preview(int index)
    {
        StopPreview();
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
                reference = EventReference.Find(eventField.FindPropertyRelative("Path").stringValue);
                break;
            }
        }
        if (reference.IsNull) { _status.text = "재생할 FMOD 이벤트를 지정해 주세요."; return; }
        EditorUtils.LoadPreviewBanks();
        FMOD.RESULT result = EditorUtils.System.getEventByID(reference.Guid, out FMOD.Studio.EventDescription description);
        if (result != FMOD.RESULT.OK) { _status.text = "이벤트 조회 실패: " + result; return; }
        result = description.isOneshot(out bool isOneShot);
        if (!isOneShot) { _status.text = "단발 이벤트만 미리 듣기에 사용할 수 있습니다."; return; }
        if (result == FMOD.RESULT.OK) result = description.createInstance(out _preview);
        if (result == FMOD.RESULT.OK) result = _preview.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));
        if (result == FMOD.RESULT.OK) result = _preview.setVolume(entry.Volume);
        if (result == FMOD.RESULT.OK) result = _preview.setPitch(entry.Pitch);
        if (result == FMOD.RESULT.OK && !string.IsNullOrEmpty(entry.ParameterName))
            result = _preview.setParameterByNameWithLabel(entry.ParameterName, entry.Label);
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
