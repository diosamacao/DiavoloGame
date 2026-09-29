
/// <summary>Selector：子节点按序，首个非 Failure 决定结果。</summary>
public sealed class SelectorNode : IBehaviorNode
{
    readonly IBehaviorNode[] _children;
    int _runningIndex;

    /// <summary>创建 Selector。</summary>
    public SelectorNode(params IBehaviorNode[] children)
    {
        _children = children ?? System.Array.Empty<IBehaviorNode>();
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        for (int i = _runningIndex; i < _children.Length; i++)
        {
            BehaviorStatus status = _children[i].Tick(blackboard);
            if (status == BehaviorStatus.Running)
            {
                _runningIndex = i;
                return BehaviorStatus.Running;
            }

            if (status == BehaviorStatus.Success)
            {
                _runningIndex = 0;
                return BehaviorStatus.Success;
            }
        }

        _runningIndex = 0;
        return BehaviorStatus.Failure;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _runningIndex = 0;
        for (int i = 0; i < _children.Length; i++)
            _children[i].Reset();
    }
}
