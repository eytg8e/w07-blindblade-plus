using UnityEngine;

/// <summary>
/// 사건의 위치·표면 법선·방향을 공통 표현 담당자에게 전달한다.
/// </summary>
public readonly struct SensoryEvent
{
    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public Vector3 Direction { get; }

    /// <summary>
    /// 사건 위치·표면 법선·진행 방향으로 표면에 정렬된 정보를 보관한다.
    /// </summary>
    public SensoryEvent(Vector3 position, Vector3 surfaceNormal, Vector3 direction)
    {
        Position = position;
        SurfaceNormal = surfaceNormal.normalized;
        Vector3 forward = Vector3.ProjectOnPlane(direction, SurfaceNormal);
        if (forward.sqrMagnitude <= Mathf.Epsilon)
            forward = Vector3.ProjectOnPlane(Vector3.forward, SurfaceNormal);
        if (forward.sqrMagnitude <= Mathf.Epsilon)
            forward = Vector3.ProjectOnPlane(Vector3.right, SurfaceNormal);
        Direction = forward.normalized;
    }
}
