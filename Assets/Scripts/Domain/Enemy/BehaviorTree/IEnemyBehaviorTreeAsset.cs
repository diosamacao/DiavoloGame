/// <summary>敌人行为树资产契约；Factory 经此创建 Runner，禁止泄漏具体后端类型。</summary>
public interface IEnemyBehaviorTreeAsset
{
    /// <summary>按装配上下文创建独立 Runner 实例（每敌一份）。</summary>
    IEnemyBehaviorRunner CreateRunner(in EnemyBehaviorBuildContext context);
}
