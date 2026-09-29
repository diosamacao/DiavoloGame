using UnityEngine;

/// <summary>子节点结束一律 Success（可选分支）。</summary>
public sealed class SucceederNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建 Succeeder。</summary>
    public SucceederNode(IBehaviorNode child)
    {
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        BehaviorStatus status = _child.Tick(blackboard);
        return status == BehaviorStatus.Running ? BehaviorStatus.Running : BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}
