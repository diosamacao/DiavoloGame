using UnityEngine;

/// <summary>行动：仅请求刷新面向目标。</summary>
public sealed class FaceTargetAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;
        blackboard.FaceTargetRequested = true;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}
