using UnityEngine;

/// <summary>招式 Rotation 窗口转向：优先索敌锁；无锁时用移动意图。</summary>
public sealed class ActionRotationDriver
{
    readonly Transform _actorRoot;
    readonly IMoveIntentSource _moveIntent;
    readonly IMoveIntentResolver _moveResolver;
    readonly ActionSim _actionSim;
    readonly CombatTargetLock targetLock;
    float _rotationVelocity;

    /// <summary>创建动作旋转服务；由 ActionState 在动作状态中调用。</summary>
    public ActionRotationDriver(
        Transform actorRoot,
        IMoveIntentSource moveIntent,
        IMoveIntentResolver moveResolver,
        ActionSim actionSim,
        CombatTargetLock lockState)
    {
        _actorRoot = actorRoot;
        _moveIntent = moveIntent;
        _moveResolver = moveResolver;
        _actionSim = actionSim;
        targetLock = lockState;
    }

    /// <summary>Action 状态下按固定逻辑步长推进索敌和旋转窗口。</summary>
    public void Tick(float fixedDeltaSeconds)
    {
        ActionSimSnapshot snapshot = _actionSim.Snapshot;
        targetLock.Tick(in snapshot);
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
        float lockSmoothTime = targetLock.ResolveLockSmoothTime(windowSmoothTime);

        bool hasLock = targetLock.TryGetLockDirection(out Vector3 lockDir);
        bool hasInput = _moveIntent.HasMoveIntent;

        if (hasLock)
        {
            // 有索敌时始终朝锁；侧移/残留 MoveIntent 不得扭开攻击朝向（AI 对峙污染）
            direction = lockDir;
            smoothTime = lockSmoothTime;
            return true;
        }

        if (!hasInput)
            return false;

        direction = _moveResolver.ResolveWorldMoveDirection(_moveIntent.MoveIntent);
        smoothTime = windowSmoothTime;
        return direction.sqrMagnitude > 0.001f;
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
