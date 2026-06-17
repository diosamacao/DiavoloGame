using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    [SerializeField] InputActionAsset inputActions = null!;

    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputAction attackAction = null!;

    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    public bool AttackPressedThisFrame => attackAction != null && attackAction.WasPressedThisFrame();

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
