using UnityEngine;

/// <summary>玩家位移执行层：InputManager 采集、Locomotion 位移与重力；招式逻辑由 CharacterActionDriver 负责。</summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputReader))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(CharacterActionDriver))]
[RequireComponent(typeof(ActionRotationDriver))]
public class PlayerController : MonoBehaviour, IActionStartContext, IMoveIntentResolver
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

    CharacterController controller;
    InputReader inputReader;
    /// <summary>玩家状态机；基类类型供 PushMotorSnapshot 与 CurrentStateType 共用。</summary>
    CharacterStateMachine stateMachine;
    CombatModeController combatMode;
    ActionRuntimeController actionRuntime;
    CharacterActionDriver actionDriver;
    ActionRotationDriver rotationDriver;

    Vector3 velocity;
    float rotationVelocity;
    float moveInputMagnitude;

    public InputManager Input => _inputManager;
    public float MoveInputMagnitude => moveInputMagnitude;
    public float RunThreshold => runThreshold;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public ICombatModeController CombatMode => combatMode;
    public float DefaultRotationSmoothTime => rotationSmoothTime;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputReader = GetComponent<InputReader>();
        stateMachine = GetComponent<PlayerStateMachine>();
        combatMode = GetComponent<CombatModeController>();
        actionRuntime = GetComponent<ActionRuntimeController>();
        actionDriver = GetComponent<CharacterActionDriver>();
        rotationDriver = GetComponent<ActionRotationDriver>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        actionDriver.BindInput(_inputManager);
        rotationDriver.Bind(_inputManager, this);
        actionRuntime.BindComboInput(actionDriver.CreateComboInputBridge());
        actionRuntime.BindActionStartContext(this);
    }

    void Update()
    {
        IngestInput();
        actionDriver.ProcessGameplayInput();
        ExecuteLocomotionMovement();
        ApplyGravity();
        // 在 StateMachine.Update（order 0）之前写入 Context；单向 PC → PSM，无反向引用。
        stateMachine.PushMotorSnapshot(moveInputMagnitude, runThreshold, IsGrounded);
    }

    /// <summary>采集本帧输入并写入 InputManager。</summary>
    void IngestInput()
    {
        _inputManager.IngestFrame(inputReader.CaptureFrame());
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

    /// <summary>Locomotion 状态下根据移动意图执行位移。</summary>
    void ExecuteLocomotionMovement()
    {
        if (stateMachine.CurrentStateType == CharacterStateType.Action)
        {
            moveInputMagnitude = 0f;
            return;
        }

        Vector2 moveIntent = _inputManager.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        moveInputMagnitude = _inputManager.MoveMagnitude;
        float speed = moveInputMagnitude > runThreshold ? runSpeed : walkSpeed;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = GetSmoothedRotation(moveDirection);
        controller.Move(moveDirection * (speed * moveInputMagnitude) * Time.deltaTime);
    }

    /// <summary>将移动意图转为世界空间方向。</summary>
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
        if (rotationSmoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

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
}
