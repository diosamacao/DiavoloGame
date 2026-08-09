/// <summary>创建 Runner 时的只读装配袋；禁止塞入 ActionExecutor / Actor 可变写引用。</summary>
public readonly struct EnemyBehaviorBuildContext
{
    /// <summary>创建装配上下文。</summary>
    public EnemyBehaviorBuildContext(EnemyBrainProfile profile, IEnemyPathQuery pathQuery)
    {
        Profile = profile;
        PathQuery = pathQuery;
    }

    /// <summary>半径/冷却等数值真源。</summary>
    public EnemyBrainProfile Profile { get; }

    /// <summary>追击方向查询；可空时 Runner 内回退直线。</summary>
    public IEnemyPathQuery PathQuery { get; }
}
