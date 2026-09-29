using UnityEngine;

/// <summary>行动：清空移动欲望。</summary>
public sealed class StopMoveAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard != null)
            blackboard.MoveDesire = Vector2.zero;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}
