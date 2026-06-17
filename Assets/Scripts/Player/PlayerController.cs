using UnityEngine;

/// <summary>玩家位移执行层：从 InputManager 读取移动意图，驱动 CharacterController。</summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputReader))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(ActionRuntimeController))]
public class PlayerController : MonoBehaviour
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
    IPlayerInputSource inputSource;
    PlayerStateMachine stateMachine;
    ActionRuntimeController actionRuntime;

    Vector3 velocity;
    float rotationVelocity;
    float moveInputMagnitude;
    bool _wasInAction;

    public InputManager Input => _inputManager;
    public float MoveInputMagnitude => moveInputMagnitude;
    public float RunThreshold => runThreshold;
    public bool IsGrounded => controller != null && controller.isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputSource = GetComponent<InputReader>();
        stateMachine = GetComponent<PlayerStateMachine>();
        actionRuntime = GetComponent<ActionRuntimeController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        actionRuntime.BindComboInput(new InputManagerComboInput(_inputManager));
        RegisterInputHandlers();
    }

    void RegisterInputHandlers()
    {
        _inputManager.RegisterPressed(InputSlot.Attack, HandleAttackPressed);
        _inputManager.RegisterPressed(InputSlot.Dodge, HandleDodgePressed);
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
        _inputManager.IngestFrame(inputSource.CaptureFrame());
    }

    void ProcessGameplayInput()
    {
        bool inAction = stateMachine.CurrentStateType == CharacterStateType.Action;
        if (_wasInAction && !inAction)
        {
            _inputManager.ClearBuffer(InputSlot.Attack);
            _inputManager.ClearBuffer(InputSlot.Dodge);
        }

        if (inAction)
            TryCancelActionByMovement();

        _wasInAction = inAction;
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

    void HandleAttackPressed()
    {
        if (stateMachine.CurrentStateType == CharacterStateType.Locomotion)
            TryStartAttackFromLocomotion();
        else if (stateMachine.CurrentStateType == CharacterStateType.Action)
            _inputManager.Buffer(InputSlot.Attack);
    }

    void HandleDodgePressed()
    {
        if (stateMachine.CurrentStateType == CharacterStateType.Locomotion)
            TryStartDodgeFromLocomotion();
        else if (stateMachine.CurrentStateType == CharacterStateType.Action)
            _inputManager.Buffer(InputSlot.Dodge);
    }

    void TryStartAttackFromLocomotion()
    {
        _inputManager.ClearBuffer(InputSlot.Attack);

        if (!actionRuntime.TryStartDefaultAction())
            return;

        stateMachine.TryChangeState(CharacterStateType.Action);
    }

    void TryStartDodgeFromLocomotion()
    {
        _inputManager.ClearBuffer(InputSlot.Dodge);
        ApplyDodgeFacing();

        if (!actionRuntime.TryStartDefaultDodge())
            return;

        stateMachine.TryChangeState(CharacterStateType.Action);
    }

    /// <summary>闪避前按缓冲/当前移动意图转向；无输入则保持面朝方向。</summary>
    void ApplyDodgeFacing()
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
}
