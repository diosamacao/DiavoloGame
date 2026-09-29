using UnityEngine;

/// <summary>行动：绕目标侧移；sideSign&gt;0 本地右，&lt;0 本地左。</summary>
public sealed class StrafeAroundTargetAction : IBehaviorNode
{
    readonly float _sideSign;
    readonly float _magnitude;

    /// <summary>创建侧移行动；magnitude 宜小于 RunThreshold 以保 Walk 档。</summary>
    public StrafeAroundTargetAction(float sideSign = 1f, float magnitude = 0.35f)
    {
        _sideSign = sideSign >= 0f ? 1f : -1f;
        _magnitude = Mathf.Clamp01(magnitude);
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;

        blackboard.FaceTargetRequested = true;
        blackboard.MoveDesire = new Vector2(_sideSign * _magnitude, 0f);
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}
