using UnityEngine;

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
    InputReader input;
    PlayerStateMachine stateMachine;
    ActionRuntimeController actionRuntime;

    Vector3 velocity;
    float rotationVelocity;
    float moveInputMagnitude;
    bool _wasInAction;

    public float MoveInputMagnitude => moveInputMagnitude;
    public float RunThreshold => runThreshold;
    public bool IsGrounded => controller != null && controller.isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<InputReader>();
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
    }

    void Update()
    {
        ProcessInput();
        UpdateMovement();
        ApplyGravity();
    }

    void ProcessInput()
    {
        if (input.AttackPressedThisFrame)
            _inputManager.NotifyPressed(InputSlot.Attack);

        bool inAction = stateMachine.CurrentStateType == CharacterStateType.Action;
        if (_wasInAction && !inAction)
            _inputManager.ClearBuffer(InputSlot.Attack);

        _wasInAction = inAction;
    }

    void HandleAttackPressed()
    {
        if (stateMachine.CurrentStateType == CharacterStateType.Locomotion)
            TryStartAttackFromLocomotion();
        else if (stateMachine.CurrentStateType == CharacterStateType.Action)
            _inputManager.Buffer(InputSlot.Attack);
    }

    void TryStartAttackFromLocomotion()
    {
        _inputManager.ClearBuffer(InputSlot.Attack);

        if (!actionRuntime.TryStartDefaultAction())
            return;

        stateMachine.TryChangeState(CharacterStateType.Action);
    }

    void UpdateMovement()
    {
        bool inAction = stateMachine.CurrentStateType == CharacterStateType.Action;

        if (!inAction)
        {
            Vector2 moveInput = input.MoveInput;
            Vector3 moveDirection = GetCameraRelativeMoveDirection(moveInput);
            moveInputMagnitude = Mathf.Clamp01(moveInput.magnitude);
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

    Vector3 GetCameraRelativeMoveDirection(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (cameraTransform == null)
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * moveInput.y + right * moveInput.x).normalized;
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
