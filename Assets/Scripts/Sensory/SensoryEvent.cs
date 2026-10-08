using System;

using UnityEngine;

public enum SensoryEventType
{
    Footstep,
    Collision,
    Sound
}

public enum FootSide
{
    None,
    Left,
    Right
}

/// <summary>
/// 발생원이 공통 표현 담당자에게 전달하는 월드 위치와 사건 정보를 보관한다.
/// </summary>
public readonly struct SensoryEvent
{
    public SensoryEventType Type { get; }
    public Transform Source { get; }
    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public Vector3 Direction { get; }
    public float Intensity { get; }
    public FootSide Side { get; }

    /// <summary>
    /// 사건 입력과 발 정보를 보관하고 표면 방향을 정규화하며 강도를 0~1로 제한한다.
    /// </summary>
    private SensoryEvent(SensoryEventType type, Transform source, Vector3 position,
        Vector3 surfaceNormal, Vector3 direction, float intensity, FootSide side)
    {
        if (float.IsNaN(intensity) || float.IsInfinity(intensity))
            throw new ArgumentOutOfRangeException(nameof(intensity), "사건 강도는 유한한 값이어야 합니다.");

        Type = type;
        Source = source;
        Position = position;
        SurfaceNormal = surfaceNormal.sqrMagnitude > Mathf.Epsilon
            ? surfaceNormal.normalized : Vector3.up;
        Vector3 surfaceDirection = Vector3.ProjectOnPlane(direction, SurfaceNormal);
        if (surfaceDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            surfaceDirection = Vector3.ProjectOnPlane(Vector3.forward, SurfaceNormal);
            if (surfaceDirection.sqrMagnitude <= Mathf.Epsilon)
                surfaceDirection = Vector3.ProjectOnPlane(Vector3.right, SurfaceNormal);
        }
        Direction = surfaceDirection.normalized;
        Intensity = Mathf.Clamp01(intensity);
        Side = side;
    }

    /// <summary>
    /// 발생 캐릭터, 지면 접촉 위치·법선·이동 방향과 강도 및 좌우 선택으로 걸음 사건을 반환한다.
    /// </summary>
    public static SensoryEvent CreateFootstep(Transform source, Vector3 position,
        Vector3 surfaceNormal, Vector3 direction, float intensity, bool isLeft)
    {
        return new SensoryEvent(SensoryEventType.Footstep, source, position,
            surfaceNormal, direction, intensity, isLeft ? FootSide.Left : FootSide.Right);
    }

    /// <summary>
    /// 발생원, 충돌 접촉 위치·법선·방향과 정규화 강도로 발 정보 없는 충돌 사건을 반환한다.
    /// </summary>
    public static SensoryEvent CreateCollision(Transform source, Vector3 position,
        Vector3 surfaceNormal, Vector3 direction, float intensity)
    {
        return new SensoryEvent(SensoryEventType.Collision, source, position,
            surfaceNormal, direction, intensity, FootSide.None);
    }

    /// <summary>
    /// 발생원과 소리 위치 및 강도로 기본 위쪽 법선·앞쪽 방향을 사용하는 소리 사건을 반환한다.
    /// </summary>
    public static SensoryEvent CreateSound(Transform source, Vector3 position, float intensity)
    {
        return new SensoryEvent(SensoryEventType.Sound, source, position,
            Vector3.up, Vector3.forward, intensity, FootSide.None);
    }
}
