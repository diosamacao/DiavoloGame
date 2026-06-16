using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputReader))]
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

    CharacterController controller;
    InputReader input;
    Animator animator;

    Vector3 velocity;
    float rotationVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<InputReader>();
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector2 moveInput = input.MoveInput;
        Vector3 moveDirection = GetCameraRelativeMoveDirection(moveInput);
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
        float speed = inputMagnitude > runThreshold ? runSpeed : walkSpeed;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = GetSmoothedRotation(moveDirection);
            controller.Move(moveDirection * (speed * inputMagnitude) * Time.deltaTime);
        }

        ApplyGravity();
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
