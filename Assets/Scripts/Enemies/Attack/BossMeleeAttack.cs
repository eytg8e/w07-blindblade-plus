/// <summary>보스의 칼날 판정과 두 박동·기합·실행음을 공통 근접 공격 흐름으로 처리한다.</summary>
public sealed class BossMeleeAttack : EnemyMeleeAttack
{
    protected override EnemySoundAction SoundAction => EnemySoundAction.Melee;
}
