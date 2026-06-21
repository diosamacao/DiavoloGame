using UnityEngine;

/// <summary>招式 RotationWindow 内转向与索敌；依赖 IMoveIntentResolver 解析移动意图。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionRuntimeController))]
[RequireComponent(typeof(CombatTargetLock))]
public class ActionRotationDriver : MonoBehaviour
{
    [SerializeField] ActionRuntimeController actionRuntime = null!;
    [SerializeField] CombatTargetLock targetLock = null!;

    CharacterStateMachine _stateMachine;
    InputManager _input;
    IMoveIntentResolver _moveResolver;
    float _rotationVelocity;

    /// <summary>注入 InputManager 与移动意图解析器（PlayerController 在 Awake 调用）。</summary>
    public void Bind(InputManager inputManager, IMoveIntentResolver moveResolver)
    {
        _input = inputManager;
        _moveResolver = moveResolver;
    }

    void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();

        if (actionRuntime == null)
            actionRuntime = GetComponent<ActionRuntimeController>();

        if (targetLock == null)
            targetLock = GetComponent<CombatTargetLock>();
    }

    void Update()
    {
        if (_stateMachine == null || _input == null || _moveResolver == null || actionRuntime == null)
            return;

        if (_stateMachine.CurrentStateType != CharacterStateType.Action)
            return;

        targetLock.Tick(actionRuntime);
        TryApplyActionRotation();
    }

    /// <summary>RotationWindow 内解析旋转方向：索敌默认，仅反向输入（Dot&lt;0）才改用输入方向。</summary>
    void TryApplyActionRotation()
    {
        if (!TryResolveActionRotationDirection(out Vector3 direction, out float smoothTime))
            return;

        transform.rotation = GetSmoothedRotation(direction, smoothTime);
    }

    /// <summary>RotationWindow 内解析最终转向方向与平滑时间。</summary>
    bool TryResolveActionRotationDirection(out Vector3 direction, out float smoothTime)
    {
        direction = Vector3.zero;
        smoothTime = _moveResolver.DefaultRotationSmoothTime;

        if (!actionRuntime.CanRotateByInput)
            return false;

        ActionDefinition action = actionRuntime.CurrentAction;
        if (action == null || !action.HasRotationWindow)
            return false;

        float windowSmoothTime = action.RotationWindow.ResolveSmoothTime(_moveResolver.DefaultRotationSmoothTime);
        float lockSmoothTime = action.HasTargetLock
            ? action.TargetLockSettings.ResolveLockSmoothTime(windowSmoothTime)
            : windowSmoothTime;

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

    /// <summary>按指定平滑时间将朝向转向 direction；smoothTime 极小时瞬时对齐。</summary>
    Quaternion GetSmoothedRotation(Vector3 direction, float smoothTime)
    {
        if (smoothTime <= 0.001f)
            return Quaternion.LookRotation(direction);

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            smoothTime);

        return Quaternion.Euler(0f, angle, 0f);
    }
}
