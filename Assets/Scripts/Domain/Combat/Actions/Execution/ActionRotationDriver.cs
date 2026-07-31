using UnityEngine;

/// <summary>招式 RotationNotifyState 窗口内转向与索敌；依赖 IMoveIntentResolver 解析移动意图。</summary>
public sealed class ActionRotationDriver
{
    readonly Transform _actorRoot;
    readonly InputManager _input;
    readonly IMoveIntentResolver _moveResolver;
    readonly ActionExecutor actionExecutor;
    readonly CombatTargetLock targetLock;
    float _rotationVelocity;

    /// <summary>创建动作旋转服务；由 ActionState 在动作状态中调用。</summary>
    public ActionRotationDriver(
        Transform actorRoot,
        InputManager inputManager,
        IMoveIntentResolver moveResolver,
        ActionExecutor executor,
        CombatTargetLock lockState)
    {
        _actorRoot = actorRoot;
        _input = inputManager;
        _moveResolver = moveResolver;
        actionExecutor = executor;
        targetLock = lockState;
    }

    /// <summary>Action 状态下按固定逻辑步长推进索敌和旋转窗口。</summary>
    public void Tick(float fixedDeltaSeconds)
    {
        ActionSession session = actionExecutor.Session;
        targetLock.Tick(session);
        TryApplyActionRotation(fixedDeltaSeconds);
    }

    /// <summary>离开 Action 时清空旋转阻尼，避免下一招继承旧角速度。</summary>
    public void Reset() => _rotationVelocity = 0f;

    /// <summary>旋转窗口内解析旋转方向：索敌默认，仅反向输入（Dot&lt;0）才改用输入方向。</summary>
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

        ActionSession session = actionExecutor.Session;
        if (!session.IsActive || !actionExecutor.CanRotateByInput)
            return false;

        ActionDefinition action = session.CurrentAction;
        RotationNotifyState rotationState =
            action.GetActiveRotationStateAtFrame(session.CurrentFrame);
        if (rotationState == null)
            return false;

        float windowSmoothTime = rotationState.ResolveSmoothTime(_moveResolver.DefaultRotationSmoothTime);
        float lockSmoothTime = targetLock.ResolveLockSmoothTime(windowSmoothTime);

        bool hasLock = targetLock.TryGetLockDirection(out Vector3 lockDir);
        bool hasInput = _input.HasMoveIntent;

        if (hasLock)
        {
            if (!hasInput)
            {
                direction = lockDir;
                smoothTime = lockSmoothTime;
                return true;
            }

            Vector3 inputDir = _moveResolver.ResolveWorldMoveDirection(_input.MoveIntent);
            if (inputDir.sqrMagnitude < 0.001f)
            {
                direction = lockDir;
                smoothTime = lockSmoothTime;
                return true;
            }

            bool useInputDirection = Vector3.Dot(inputDir, lockDir) < 0f;
            direction = useInputDirection ? inputDir : lockDir;
            smoothTime = useInputDirection ? windowSmoothTime : lockSmoothTime;
            return true;
        }

        if (!hasInput)
            return false;

        direction = _moveResolver.ResolveWorldMoveDirection(_input.MoveIntent);
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
