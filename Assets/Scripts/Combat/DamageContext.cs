using UnityEngine;

public readonly struct DamageContext
{
    public float Amount { get; }
    public GameObject Instigator { get; }
    public Vector3 Point { get; }
    public Vector3 Direction { get; }
    public string PatternId { get; }

    /// <summary>
    /// amount, instigator, point, direction, patternId를 저장하여 피해 수신자에게 전달할 문맥을 만든다.
    /// 피해량, 공격자, 타격 위치와 방향 및 공격 패턴 식별자를 반환 객체에 보관한다.
    /// </summary>
    public DamageContext(float amount, GameObject instigator, Vector3 point, Vector3 direction, string patternId)
    {
        Amount = amount;
        Instigator = instigator;
        Point = point;
        Direction = direction;
        PatternId = patternId;
    }
}
