using UnityEngine;

/// <summary>
/// 걸음의 지면 위치·방향과 좌우 선택을 공통 표현 담당자에게 전달한다.
/// </summary>
public readonly struct SensoryEvent
{
    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public Vector3 Direction { get; }
    public bool IsLeft { get; }

    /// <summary>
    /// 지면 위치·법선·진행 방향과 좌우 선택으로 정렬된 걸음 정보를 보관한다.
    /// </summary>
    public SensoryEvent(Vector3 position, Vector3 surfaceNormal, Vector3 direction, bool isLeft)
    {
        Position = position;
        SurfaceNormal = surfaceNormal.normalized;
        Vector3 forward = Vector3.ProjectOnPlane(direction, SurfaceNormal);
        if (forward.sqrMagnitude <= Mathf.Epsilon)
            forward = Vector3.ProjectOnPlane(Vector3.forward, SurfaceNormal);
        if (forward.sqrMagnitude <= Mathf.Epsilon)
            forward = Vector3.ProjectOnPlane(Vector3.right, SurfaceNormal);
        Direction = forward.normalized;
        IsLeft = isLeft;
    }
}
