/// <summary>
/// 敌人行为决策运行时契约；Brain 只依赖本接口，便于日后替换为插件 Adapter。
/// </summary>
public interface IEnemyBehaviorRunner
{
    /// <summary>门闩抢占或重进战时清空 Running 索引。</summary>
    void Reset();

    /// <summary>逻辑帧 Tick；只读写黑板，禁止起招或改 Numeric。</summary>
    BehaviorStatus Tick(EnemyBlackboard blackboard);
}
