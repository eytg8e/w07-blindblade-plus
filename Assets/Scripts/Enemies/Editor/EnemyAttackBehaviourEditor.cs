using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

[CustomEditor(typeof(EnemyAttackBehaviour), true)]
[CanEditMultipleObjects]
public sealed class EnemyAttackBehaviourEditor : Editor
{
    /// <summary>
    /// 현재 공격 컴포넌트의 기본 Inspector와 계산된 공격 간격 표시를 반환한다.
    /// 설정값과 최근 공격 문맥을 읽어 표시하며 게임 상태나 직렬화된 값을 변경하지 않는다.
    /// </summary>
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);
        foreach (PropertyField field in root.Query<PropertyField>().ToList())
        {
            if (field.bindingPath == "_useHeartbeatTelegraph") field.label = "박동 사용";
            else if (field.bindingPath == "_firstHeartbeatDelay") field.label = "첫 박동 전 대기 (초)";
            else if (field.bindingPath == "_heartbeatCount") field.label = "박동 횟수";
            else if (field.bindingPath == "_minimumHeartbeatInterval") field.label = "박동 간격 최소 (초)";
            else if (field.bindingPath == "_maximumHeartbeatInterval") field.label = "박동 간격 최대 (초)";
            else if (field.bindingPath == "_postHeartbeatAttackDelay") field.label = "마지막 박동 후 공격 지연 (초)";
        }
        var interval = new Label { name = "expectedAttackInterval" };
        var rhythm = new Label { name = "heartbeatTiming" };
        var chosen = new Label { name = "chosenTelegraphDuration" };
        root.Add(interval);
        root.Add(rhythm);
        root.Add(chosen);
        root.Add(new HelpBox("반복 간격은 전조·최대 실행·기본 후딜과 쿨타임으로 계산합니다. 실제 접촉·피격·패링에 따라 달라집니다.", HelpBoxMessageType.Info));
        root.schedule.Execute(() => UpdateTimingLabels(interval, rhythm, chosen)).Every(250);
        UpdateTimingLabels(interval, rhythm, chosen);
        return root;
    }

    /// <summary>
    /// 현재 target의 계산된 반복 간격과 최근 전조 시간을 interval·rhythm·chosen에 표시한다.
    /// 공격 준비 시간과 박동 간격의 차이를 설명하며 컴포넌트가 없어지면 갱신하지 않는다.
    /// </summary>
    private void UpdateTimingLabels(Label interval, Label rhythm, Label chosen)
    {
        if (target == null) return;
        var attack = (EnemyAttackBehaviour)target;
        Vector2 expected = attack.ExpectedAttackInterval;
        interval.text = (targets.Length > 1 ? "첫 선택 대상의 " : "") + $"예상 반복 공격 간격: {expected.x:F2} ~ {expected.y:F2}초";
        rhythm.text = attack.UsesHeartbeatTelegraph
            ? $"첫 박동 전 고정 대기 → 박동 {attack.HeartbeatCount}회 → 마지막 박동 후 고정 지연 → 공격"
            : "기존 고정 전조 사용";
        float duration = attack.Context.TelegraphDuration;
        chosen.text = duration > 0f
            ? $"최근 선택 전조: {duration:F2}초" + (attack.UsesHeartbeatTelegraph ? $" / 박동 간격: {attack.LastHeartbeatInterval:F2}초" : "")
            : (attack.Context.Pattern != null ? "최근 공격: 추격 도착 즉시 공격 (박동 생략)" : "최근 선택 전조: 공격 시작 후 표시");
    }
}
