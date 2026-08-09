/// <summary>自研行为树根；包装根节点 Tick/Reset。</summary>
public sealed class BehaviorTree
{
    readonly IBehaviorNode _root;

    /// <summary>以根节点构造树。</summary>
    public BehaviorTree(IBehaviorNode root)
    {
        _root = root ?? throw new System.ArgumentNullException(nameof(root));
    }

    /// <summary>从根 Tick 一整棵树。</summary>
    public BehaviorStatus Tick(EnemyBlackboard blackboard) => _root.Tick(blackboard);

    /// <summary>重置整棵树的 Running 状态。</summary>
    public void Reset() => _root.Reset();
}
