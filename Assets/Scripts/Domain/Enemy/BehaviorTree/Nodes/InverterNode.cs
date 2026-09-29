using UnityEngine;

/// <summary>反转 Success/Failure；Running 透传。</summary>
public sealed class InverterNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建 Inverter。</summary>
    public InverterNode(IBehaviorNode child)
    {
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        BehaviorStatus status = _child.Tick(blackboard);
        if (status == BehaviorStatus.Running)
            return BehaviorStatus.Running;
        return status == BehaviorStatus.Success ? BehaviorStatus.Failure : BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}
