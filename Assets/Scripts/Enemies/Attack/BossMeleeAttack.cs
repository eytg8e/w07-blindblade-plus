/// <summary>보스의 칼날 판정은 기존 근접 공격을 사용하고 전조 소리는 보스 표현 담당자에게 맡긴다.</summary>
public sealed class BossMeleeAttack : EnemyMeleeAttack
{
    protected override EnemySoundAction SoundAction => EnemySoundAction.None;
}
