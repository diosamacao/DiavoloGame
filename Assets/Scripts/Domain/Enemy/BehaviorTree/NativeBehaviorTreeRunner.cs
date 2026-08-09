/// <summary>自研 BT 的 IEnemyBehaviorRunner 实现；包装 BehaviorTree。</summary>
public sealed class NativeBehaviorTreeRunner : IEnemyBehaviorRunner
{
    readonly BehaviorTree _tree;

    /// <summary>以根节点创建 Runner。</summary>
    public NativeBehaviorTreeRunner(IBehaviorNode root)
    {
        _tree = new BehaviorTree(root);
    }

    /// <inheritdoc />
    public void Reset() => _tree.Reset();

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) => _tree.Tick(blackboard);
}
