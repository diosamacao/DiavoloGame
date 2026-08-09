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

/// <summary>行动：朝目标前进（贴近 StopDistance 时停步仍 Success）。</summary>
public sealed class MoveTowardTargetAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Profile == null || !blackboard.HasTarget)
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
        blackboard.FaceTargetRequested = blackboard.Profile.FaceTargetWhileChase;

        if (blackboard.PlanarDistance <= blackboard.Profile.StopDistance)
        {
            blackboard.MoveDesire = Vector2.zero;
            return BehaviorStatus.Success;
        }

        // 与旧 FSM 一致：假相机朝向目标后，本地前进轴写 (0, magnitude)
        blackboard.MoveDesire = Vector2.up * blackboard.Profile.ChaseMoveMagnitude;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：背离目标后退（面向目标后本地 y&lt;0）。</summary>
public sealed class BackOffFromTargetAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Profile == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;

        blackboard.FaceTargetRequested = true;
        blackboard.MoveDesire = Vector2.down * blackboard.Profile.ChaseMoveMagnitude;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：绕目标侧移；sideSign&gt;0 本地右，&lt;0 本地左。</summary>
public sealed class StrafeAroundTargetAction : IBehaviorNode
{
    readonly float _sideSign;

    /// <summary>创建侧移行动；sideSign 符号决定左右。</summary>
    public StrafeAroundTargetAction(float sideSign = 1f)
    {
        _sideSign = sideSign >= 0f ? 1f : -1f;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Profile == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;

        blackboard.FaceTargetRequested = true;
        // 对峙用独立幅度，便于压在 Walk 档并驱动 WalkLeft/Right
        float magnitude = blackboard.Profile.StrafeMoveMagnitude;
        blackboard.MoveDesire = new Vector2(_sideSign * magnitude, 0f);
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

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

/// <summary>行动：请求本帧攻击脉冲（由 Brain 提交到 AIInputWriter）。</summary>
public sealed class PulseAttackAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;
        blackboard.AttackPulse = true;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：请求本帧闪避脉冲。</summary>
public sealed class PulseDodgeAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;
        blackboard.DodgePulse = true;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：请求本帧重击脉冲。</summary>
public sealed class PulseHeavyAttackAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;
        blackboard.HeavyAttackPulse = true;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：请求本帧特殊/技能脉冲。</summary>
public sealed class PulseSkillAction : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;
        blackboard.SkillPulse = true;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>行动：跨逻辑帧等待（Running → Success）；durationFrames 可配。</summary>
public sealed class WaitFramesAction : IBehaviorNode
{
    readonly int _durationFrames;
    int _remaining;

    /// <summary>创建按逻辑帧等待的节点。</summary>
    public WaitFramesAction(int durationFrames)
    {
        _durationFrames = Mathf.Max(1, durationFrames);
        _remaining = _durationFrames;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        _remaining--;
        if (_remaining > 0)
            return BehaviorStatus.Running;
        _remaining = _durationFrames;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _remaining = _durationFrames;
}
