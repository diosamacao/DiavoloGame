using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家位移执行层：从 InputManager 读取移动意图，驱动 CharacterController。</summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputReader))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(CombatModeController))]
[RequireComponent(typeof(ActionRuntimeController))]
public class PlayerController : MonoBehaviour, IActionStartContext
{
    [Header("Movement")]
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float runSpeed = 7f;
    [SerializeField] float runThreshold = 0.6f;
    [SerializeField] float rotationSmoothTime = 0.12f;

    [Header("Gravity")]
    [SerializeField] float gravity = -20f;
    [SerializeField] float groundedGravity = -2f;

    [Header("References")]
    [SerializeField] Transform cameraTransform;

    readonly InputManager _inputManager = new();
    readonly HashSet<string> _registeredInputIds = new();

    CharacterController controller;
    InputReader inputReader;
    PlayerStateMachine stateMachine;
    CombatModeController combatMode;
    ActionRuntimeController actionRuntime;

    Vector3 velocity;
    float rotationVelocity;
    float moveInputMagnitude;
    bool _wasInAction;

    public InputManager Input => _inputManager;
    public float MoveInputMagnitude => moveInputMagnitude;
    public float RunThreshold => runThreshold;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public ICombatModeController CombatMode => combatMode;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputReader = GetComponent<InputReader>();
        stateMachine = GetComponent<PlayerStateMachine>();
        combatMode = GetComponent<CombatModeController>();
        actionRuntime = GetComponent<ActionRuntimeController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        inputReader.ConfigureDiscreteInputs(actionRuntime.GetEntryInputReferences());
        actionRuntime.BindComboInput(new PlayerComboInput(_inputManager));
        actionRuntime.BindActionStartContext(this);
        RegisterInputHandlers();
    }

    /// <summary>注册全部战斗模式出招表中的离散输入（并集，按 inputId 去重）。</summary>
    void RegisterInputHandlers()
    {
        _registeredInputIds.Clear();

        if (combatMode.Profile == null)
        {
            Debug.LogWarning("PlayerController: CombatModeProfile 未绑定，离散输入未注册。", this);
            return;
        }

        bool hasAnyEntry = false;
        foreach (ActionEntry entry in combatMode.Profile.EnumerateAllActionEntries())
        {
            if (!entry.IsValid)
                continue;

            hasAnyEntry = true;
            string inputId = entry.InputId;
            if (!_registeredInputIds.Add(inputId))
                continue;

            _inputManager.RegisterPressed(inputId, () => HandleDiscreteInput(inputId));
        }

        if (!hasAnyEntry)
        {
            Debug.LogWarning(
                "PlayerController: CombatModeProfile 中无有效 ActionEntry，攻击/闪避输入未注册。请配置各模式的出招表。",
                this);
        }
    }

    void Update()
    {
        IngestInput();
        ProcessGameplayInput();
        ExecuteMovement();
        ApplyGravity();
    }

    /// <summary>采集本帧输入并写入 InputManager（回放/网络可替换 inputSource 或直调 IngestFrame）。</summary>
    void IngestInput()
    {
        _inputManager.IngestFrame(inputReader.CaptureFrame());
    }

    void ProcessGameplayInput()
    {
        bool inAction = stateMachine.CurrentStateType == CharacterStateType.Action;
        if (_wasInAction && !inAction)
        {
            // 先应用挂起的 mode（OnNextLocomotion），再消费 Switch 期间的预输入
            CombatMode.ApplyPendingModeIfReady();
            if (!TryStartFromBufferedInputs())
                ClearAllActionBuffers();
        }

        if (inAction)
            TryCancelActionByMovement();

        _wasInAction = inAction;
    }

    /// <summary>离开 Action 后尝试用缓冲的离散输入从 Locomotion 起手。</summary>
    bool TryStartFromBufferedInputs()
    {
        PlayerActionSet actionSet = combatMode.ActiveActionSet;
        if (actionSet == null)
            return false;

        foreach (ActionEntry entry in actionSet.Entries)
        {
            if (!entry.IsValid)
                continue;

            string inputId = entry.InputId;
            if (!_inputManager.HasBuffer(inputId))
                continue;

            TryStartFromLocomotion(inputId);
            return stateMachine.CurrentStateType == CharacterStateType.Action;
        }

        return false;
    }

    void ClearAllActionBuffers()
    {
        if (combatMode.Profile == null)
            return;

        foreach (ActionEntry entry in combatMode.Profile.EnumerateAllActionEntries())
        {
            if (entry.IsValid)
                _inputManager.ClearBuffer(entry.InputId);
        }
    }

    /// <summary>移动取消：读取移动意图（非位移执行），在取消窗口内退回 Locomotion。</summary>
    void TryCancelActionByMovement()
    {
        if (!_inputManager.HasMoveIntent)
            return;

        if (!actionRuntime.CanCancelByMovement)
            return;

        stateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    void HandleDiscreteInput(string inputId)
    {
        if (stateMachine.CurrentStateType == CharacterStateType.Locomotion)
            TryStartFromLocomotion(inputId);
        else if (stateMachine.CurrentStateType == CharacterStateType.Action)
            _inputManager.Buffer(inputId);
    }

    void TryStartFromLocomotion(string inputId)
    {
        _inputManager.ClearBuffer(inputId);

        if (!actionRuntime.TryStartByInput(inputId))
            return;

        stateMachine.TryChangeState(CharacterStateType.Action);
    }

    public void FaceBufferedMoveIntent()
    {
        Vector2 moveIntent = _inputManager.HasMoveIntent
            ? _inputManager.MoveIntent
            : _inputManager.BufferedMoveIntent;

        Vector3 direction = ResolveWorldMoveDirection(moveIntent);
        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    /// <summary>根据当前移动意图执行位移；招式状态中不执行，但意图仍由 InputManager 持续更新/缓冲。</summary>
    void ExecuteMovement()
    {
        bool inAction = stateMachine.CurrentStateType == CharacterStateType.Action;

        if (!inAction)
        {
            Vector2 moveIntent = _inputManager.MoveIntent;
            Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
            moveInputMagnitude = _inputManager.MoveMagnitude;
            float speed = moveInputMagnitude > runThreshold ? runSpeed : walkSpeed;

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = GetSmoothedRotation(moveDirection);
                controller.Move(moveDirection * (speed * moveInputMagnitude) * Time.deltaTime);
            }
        }
        else
        {
            moveInputMagnitude = 0f;
        }
    }

    /// <summary>将移动意图转为世界空间方向；闪避等招式可复用 BufferedMoveIntent 调用此方法。</summary>
    public Vector3 ResolveWorldMoveDirection(Vector2 moveIntent)
    {
        if (moveIntent.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (cameraTransform == null)
            return new Vector3(moveIntent.x, 0f, moveIntent.y).normalized;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * moveIntent.y + right * moveIntent.x).normalized;
    }

    Quaternion GetSmoothedRotation(Vector3 moveDirection)
    {
        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime);

        return Quaternion.Euler(0f, angle, 0f);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = groundedGravity;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>将本控制器 InputManager 缓冲桥接给 ActionRuntime Cancel 消费。</summary>
    sealed class PlayerComboInput : IActionComboInput
    {
        readonly InputManager _inputManager;

        public PlayerComboInput(InputManager inputManager) => _inputManager = inputManager;

        public bool HasBuffer(string inputId) => _inputManager.HasBuffer(inputId);

        public bool TryConsumeBuffer(string inputId) => _inputManager.TryConsumeBuffer(inputId);
    }
}
