using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>从 Input System 采集本帧原始输入，供 InputManager 摄入。</summary>
public class InputReader : MonoBehaviour, IPlayerInputSource
{
    [SerializeField] InputActionAsset inputActions = null!;

    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputActionReference[] _discreteInputs = Array.Empty<InputActionReference>();
    bool _initialized;

    readonly System.Collections.Generic.List<string> _pressedScratch = new(4);

    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

    /// <summary>由 PlayerController 根据 CharacterConfig 注入输入资产，支持 Empty GameObject 运行时装配。</summary>
    public void BindInputActions(InputActionAsset actions)
    {
        inputActions = actions;
        InitializeActions(logMissingAsset: true);
        if (_initialized && isActiveAndEnabled)
            inputActions.Enable();
    }

    /// <summary>由 PlayerController 根据 PlayerActionSet.entries 注入，无需在 Prefab 重复配置。</summary>
    public void ConfigureDiscreteInputs(InputActionReference[] references)
    {
        _discreteInputs = references ?? Array.Empty<InputActionReference>();
    }

    public PlayerInputFrame CaptureFrame()
    {
        _pressedScratch.Clear();

        foreach (InputActionReference reference in _discreteInputs)
        {
            if (!InputBindingUtils.IsValid(reference))
                continue;

            if (reference.action.WasPressedThisFrame())
                _pressedScratch.Add(reference.action.name);
        }

        return new PlayerInputFrame(MoveInput, LookInput, _pressedScratch.ToArray());
    }

    void Awake()
    {
        InitializeActions(logMissingAsset: false);
    }

    void Start()
    {
        if (!_initialized)
            InitializeActions(logMissingAsset: true);
    }

    /// <summary>解析 Player ActionMap；配置缺失时仅在最终校验阶段报错。</summary>
    void InitializeActions(bool logMissingAsset)
    {
        if (inputActions == null)
        {
            if (logMissingAsset)
            {
                Debug.LogError("InputReader: 未分配 InputActionAsset。", this);
                enabled = false;
            }

            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
        _initialized = true;
        enabled = true;
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
