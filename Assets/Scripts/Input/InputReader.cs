using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>从 Input System 采集本帧原始输入，供 InputManager 摄入。</summary>
public class InputReader : MonoBehaviour, IPlayerInputSource
{
    [SerializeField] InputActionAsset inputActions = null!;

    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputAction attackAction = null!;
    InputAction dodgeAction = null!;

    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    public bool AttackPressedThisFrame => attackAction != null && attackAction.WasPressedThisFrame();
    public bool DodgePressedThisFrame => dodgeAction != null && dodgeAction.WasPressedThisFrame();

    public PlayerInputFrame CaptureFrame() =>
        new(MoveInput, LookInput, AttackPressedThisFrame, DodgePressedThisFrame);

    void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputReader: 未分配 InputActionAsset。", this);
            enabled = false;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
        attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
        dodgeAction = playerMap.FindAction("Dodge", throwIfNotFound: true);
    }

    void OnEnable()
    {
        inputActions?.Enable();
    }

    void OnDisable()
    {
        inputActions?.Disable();
    }
}
