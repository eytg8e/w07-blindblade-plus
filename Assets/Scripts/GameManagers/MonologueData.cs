using UnityEngine;

/// <summary>여러 Trigger에서 재사용하는 독백 대사만 저장한다. 재생 상태는 에셋에 저장하지 않는다.</summary>
[CreateAssetMenu(fileName = "MonologueData", menuName = "Game/Monologue Data")]
public sealed class MonologueData : ScriptableObject
{
    [Header("대사 순서")]
    [SerializeField, TextArea(2, 5)] private string[] _lines = new string[0];

    public int LineCount => _lines == null ? 0 : _lines.Length;

    /// <summary>index에 해당하는 대사 문자열을 반환한다. 호출자는 유효한 대사 번호를 전달한다.</summary>
    public string GetLine(int index)
    {
        return _lines[index];
    }
}
