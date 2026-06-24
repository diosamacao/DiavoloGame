/// <summary>索敌目标的选择策略。</summary>
public enum TargetSelectionPolicy
{
    /// <summary>范围内距离最近的目标。</summary>
    NearestDistance = 0,

    /// <summary>范围内当前生命值最低的目标；同血量时比距离。</summary>
    LowestHealth = 1,

    /// <summary>攻击者前方扇形范围内距离最近的目标。</summary>
    NearestInForwardCone = 2,
}
