using UnityEngine;

/// <summary>行动：朝目标前进（贴近 stopDistance 时停步仍 Success）；幅度/停步/朝向由节点参数决定。</summary>
public sealed class MoveTowardTargetAction : IBehaviorNode
{
    readonly float _magnitude;
    readonly float _stopDistance;
    readonly bool _faceTarget;

    /// <summary>创建追击移动；magnitude 为本地前进轴幅度。</summary>
    public MoveTowardTargetAction(float magnitude = 1f, float stopDistance = 1.2f, bool faceTarget = true)
    {
        _magnitude = Mathf.Clamp01(magnitude);
        _stopDistance = Mathf.Max(0f, stopDistance);
        _faceTarget = faceTarget;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;

        Vector3 steer = blackboard.PathDirection.sqrMagnitude > 0.0001f
            ? blackboard.PathDirection
            : blackboard.PlanarDirection;
        if (steer.sqrMagnitude <= 0.0001f && blackboard.PathQuery != null)
        {
            steer = blackboard.PathQuery.GetSteerDirection(
                Vector3.zero,
                Vector3.zero,
                blackboard.PlanarDirection);
        }

        blackboard.PathDirection = steer;
        blackboard.FaceTargetRequested = _faceTarget;

        if (blackboard.PlanarDistance <= _stopDistance)
        {
            blackboard.MoveDesire = Vector2.zero;
            return BehaviorStatus.Success;
        }

        // 假相机朝向目标后，本地前进轴写 (0, magnitude)
        blackboard.MoveDesire = Vector2.up * _magnitude;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}
