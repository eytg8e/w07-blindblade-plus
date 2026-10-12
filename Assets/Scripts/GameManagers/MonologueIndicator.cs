using UnityEngine;

using UnityEngine.UI;

/// <summary>다음 독백 또는 마지막 독백을 삼각형 방향과 깜빡임으로 표시한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MonologueIndicator : MaskableGraphic
{
    [Header("깜빡임")]
    [SerializeField, Min(0.1f)] private float _blinkInterval = 0.45f;
    private float _elapsed;
    private bool _isLastLine;

    protected override void OnEnable()
    {
        base.OnEnable();
        _elapsed = 0f;
        canvasRenderer.SetAlpha(1f);
    }

    void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        canvasRenderer.SetAlpha(Mathf.Repeat(_elapsed, _blinkInterval * 2f) < _blinkInterval ? 1f : 0f);
    }

    /// <summary>isLastLineで最後の独白かを指定し、三角形を下向きまたは右向きに変更する。</summary>
    public void SetLastLine(bool isLastLine)
    {
        _isLastLine = isLastLine;
        _elapsed = 0f;
        canvasRenderer.SetAlpha(1f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (_isLastLine)
        {
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.center.x, rect.yMin), color, Vector2.zero);
        }
        else
        {
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.center.y), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        }
        vh.AddTriangle(0, 1, 2);
    }
}
