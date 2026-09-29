using UnityEngine;

/// <summary>行动：背离目标后退（面向目标后本地 y&lt;0）。</summary>
public sealed class BackOffFromTargetAction : IBehaviorNode
{
    readonly float _magnitude;

    /// <summary>创建后退行动。</summary>
    public BackOffFromTargetAction(float magnitude = 1f)
    {
        _magnitude = Mathf.Clamp01(magnitude);
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;

        blackboard.FaceTargetRequested = true;
        blackboard.MoveDesire = Vector2.down * _magnitude;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}
