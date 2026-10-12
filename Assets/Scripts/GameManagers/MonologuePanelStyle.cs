using UnityEngine;

using UnityEngine.UI;

/// <summary>독백 패널의 얇은 테두리와 반투명 수직 그라데이션을 그린다.</summary>
public sealed class MonologuePanelStyle : BaseMeshEffect
{
    [Header("배경 그라데이션")]
    [SerializeField] private Color _topColor = new Color32(6, 6, 6, 217);
    [SerializeField] private Color _bottomColor = new Color32(26, 26, 26, 153);

    [Header("테두리")]
    [SerializeField] private Color _outerColor = new Color32(119, 119, 119, 255);
    [SerializeField] private Color _innerColor = new Color32(36, 36, 36, 255);

    /// <summary>vh의 기본 채움 메시를 테두리와 위에서 아래로 밝아지는 배경 메시로 교체한다.</summary>
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        Rect rect = ((RectTransform)transform).rect;
        vh.Clear();
        AddBorder(vh, rect, 2f, _outerColor);
        rect = Rect.MinMaxRect(rect.xMin + 2f, rect.yMin + 2f, rect.xMax - 2f, rect.yMax - 2f);
        AddBorder(vh, rect, 1f, _innerColor);
        rect = Rect.MinMaxRect(rect.xMin + 1f, rect.yMin + 1f, rect.xMax - 1f, rect.yMax - 1f);
        AddQuad(vh, rect, _bottomColor, _topColor);
    }

    /// <summary>rect 가장자리의 width 너비를 color로 채우는 네 개의 사각형을 vh에 추가한다.</summary>
    private void AddBorder(VertexHelper vh, Rect rect, float width, Color color)
    {
        AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, rect.yMin + width), color, color);
        AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMax - width, rect.xMax, rect.yMax), color, color);
        AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMin + width, rect.xMin + width, rect.yMax - width), color, color);
        AddQuad(vh, Rect.MinMaxRect(rect.xMax - width, rect.yMin + width, rect.xMax, rect.yMax - width), color, color);
    }

    /// <summary>rect의 아래쪽은 bottom, 위쪽은 top 색인 사각형 정점과 삼각형을 vh에 추가한다.</summary>
    private void AddQuad(VertexHelper vh, Rect rect, Color bottom, Color top)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), bottom, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), top, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), top, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), bottom, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
