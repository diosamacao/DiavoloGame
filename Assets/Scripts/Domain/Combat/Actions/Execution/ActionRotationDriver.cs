using UnityEngine;

/// <summary>招式 Rotation 窗口转向：优先读取唯一 SelectedTarget；无目标策略时用移动意图。</summary>
public sealed class ActionRotationDriver
{
    readonly Transform _actorRoot;
    readonly IMoveIntentSource _moveIntent;
    readonly IMoveIntentResolver _moveResolver;
    readonly ActionSim _actionSim;
    readonly CharacterTargetingState _targetingState;
    readonly CharacterMotorSim _motor;
    float _rotationVelocity;

    /// <summary>创建动作旋转服务；由 ActionState 在动作状态中调用。</summary>
    public ActionRotationDriver(
        Transform actorRoot,
        IMoveIntentSource moveIntent,
        IMoveIntentResolver moveResolver,
        ActionSim actionSim,
        CharacterTargetingState targetingState,
        CharacterMotorSim motor)
    {
        _actorRoot = actorRoot;
        _moveIntent = moveIntent;
        _moveResolver = moveResolver;
        _actionSim = actionSim;
        _targetingState = targetingState;
        _motor = motor;
    }

    /// <summary>Action 状态下按固定逻辑步长推进旋转窗口。</summary>
    public void Tick(float fixedDeltaSeconds)
    {
        // 不在 Rotation 窗口或无方向时内部直接返回
        TryApplyActionRotation(fixedDeltaSeconds);
    }

    /// <summary>离开 Action 时清空旋转阻尼，避免下一招继承旧角速度。</summary>
    public void Reset() => _rotationVelocity = 0f;

    /// <summary>旋转窗口内解析旋转方向：有索敌锁则始终朝锁；无锁才跟移动输入。</summary>
    void TryApplyActionRotation(float fixedDeltaSeconds)
    {
        if (!TryResolveActionRotationDirection(out Vector3 direction, out float smoothTime))
            return;

        _actorRoot.rotation = GetSmoothedRotation(direction, smoothTime, fixedDeltaSeconds);
    }

    /// <summary>旋转窗口内解析最终转向方向与平滑时间。</summary>
    bool TryResolveActionRotationDirection(out Vector3 direction, out float smoothTime)
    {
        direction = Vector3.zero;
        smoothTime = _moveResolver.DefaultRotationSmoothTime;

        ActionSimSnapshot snapshot = _actionSim.Snapshot;
        ActionDefinition action = snapshot.Content as ActionDefinition;
        if (!snapshot.IsActive
            || action == null
            || !action.IsInRotationWindow(snapshot.CurrentFrame))
            return false;

        RotationNotifyState rotationState =
            action.GetActiveRotationStateAtFrame(snapshot.CurrentFrame);
        if (rotationState == null)
            return false;

        float windowSmoothTime = rotationState.ResolveSmoothTime(_moveResolver.DefaultRotationSmoothTime);
        bool consumesTarget = TryResolveTargetSmoothTime(
            in snapshot,
            windowSmoothTime,
            out float targetSmoothTime);
        Vector3 targetDirection = Vector3.zero;
        bool hasTarget = consumesTarget
            && _targetingState != null
            && _targetingState.TryGetSelectedDirection(_motor, out targetDirection);
        bool hasInput = _moveIntent.HasMoveIntent;

        if (hasTarget)
        {
            // 有目标策略时始终朝当前 SelectedTarget；同帧切敌后的方向不会被侧移输入扭开。
            direction = targetDirection;
            smoothTime = targetSmoothTime;
            return true;
        }

        if (!hasInput)
            return false;

        direction = _moveResolver.ResolveWorldMoveDirection(_moveIntent.MoveIntent);
        smoothTime = windowSmoothTime;
        return direction.sqrMagnitude > 0.001f;
    }

    /// <summary>读取当前 Graph 节点是否消费 SelectedTarget，并解析其转向平滑覆盖。</summary>
    static bool TryResolveTargetSmoothTime(
        in ActionSimSnapshot snapshot,
        float windowSmoothTime,
        out float smoothTime)
    {
        smoothTime = windowSmoothTime;
        if (snapshot.Graph is not ActionGraph graph
            || !graph.TryGetNode(snapshot.NodeId, out ActionGraphNode node)
            || node == null
            || !node.HasTargetLock)
        {
            return false;
        }

        smoothTime = node.TargetLockSettings.ResolveLockSmoothTime(windowSmoothTime);
        return true;
    }

    /// <summary>按指定平滑时间和固定步长转向；smoothTime 极小时瞬时对齐。</summary>
    Quaternion GetSmoothedRotation(
        Vector3 direction,
        float smoothTime,
        float fixedDeltaSeconds)
    {
        if (smoothTime <= 0.001f)
        {
            _rotationVelocity = 0f;
            return Quaternion.LookRotation(direction);
        }

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            _actorRoot.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            smoothTime,
            Mathf.Infinity,
            Mathf.Max(0f, fixedDeltaSeconds));

        return Quaternion.Euler(0f, angle, 0f);
    }

}
