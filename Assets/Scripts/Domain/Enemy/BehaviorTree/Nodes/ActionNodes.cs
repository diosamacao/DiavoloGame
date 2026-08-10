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

/// <summary>行动：请求按 Graph Entry NodeId 起手（Brain → CombatRequestBuffer → Driver）。</summary>
public sealed class RequestCombatAction : IBehaviorNode
{
    readonly string _entryNodeId;

    /// <summary>创建 Entry 起手请求；entryNodeId 不可空。</summary>
    public RequestCombatAction(string entryNodeId)
    {
        _entryNodeId = entryNodeId ?? string.Empty;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || string.IsNullOrEmpty(_entryNodeId))
            return BehaviorStatus.Failure;

        blackboard.HasCombatRequest = true;
        blackboard.CombatRequestEntryId = _entryNodeId;
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

/// <summary>
/// 行动：占用 BT 直至离开 Action（含 CombatRequest 当帧与 AttackConfirmPending）。
/// 等待期间清空 MoveDesire，避免对峙侧移污染攻击旋转。
/// </summary>
public sealed class WaitWhileInActionAction : IBehaviorNode
{
    bool _latched;

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;

        // 起手当帧尚未进 Action：靠 CombatRequest / ConfirmPending 闩住
        bool busy = blackboard.HasCombatRequest
            || blackboard.AttackConfirmPending
            || blackboard.CharacterState == CharacterStateType.Action;

        if (busy)
        {
            _latched = true;
            blackboard.MoveDesire = Vector2.zero;
            blackboard.FaceTargetRequested = true;
            return BehaviorStatus.Running;
        }

        if (_latched)
        {
            // 已进过招并回到可移动态
            _latched = false;
            blackboard.MoveDesire = Vector2.zero;
            return BehaviorStatus.Success;
        }

        // 未闩住且不在 Action：视为空等（脉冲未发出）
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _latched = false;
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
