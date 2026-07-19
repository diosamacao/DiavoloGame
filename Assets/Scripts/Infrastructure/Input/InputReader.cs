using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>从 Input System 采集本帧原始输入，供 InputManager 摄入。</summary>
public sealed class InputReader : ICharacterInputSource
{
    InputActionAsset inputActions = null!;
    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputActionReference[] _discreteInputs = Array.Empty<InputActionReference>();

    readonly System.Collections.Generic.List<string> _pressedScratch = new(4);
    readonly System.Collections.Generic.List<string> _heldScratch = new(4);
    readonly System.Collections.Generic.List<string> _releasedScratch = new(4);

    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

    /// <summary>创建纯 C# 输入源；由 PlayerController 按 CharacterConfig 构造。</summary>
    public InputReader(InputActionAsset actions)
    {
        inputActions = actions;
        InitializeActions();
    }

    /// <summary>由 PlayerController 根据 PlayerActionSet.entries 注入，无需在 Prefab 重复配置。</summary>
    public void ConfigureDiscreteInputs(InputActionReference[] references)
    {
        _discreteInputs = references ?? Array.Empty<InputActionReference>();
    }

    /// <summary>采集连续轴与离散输入的 Pressed/IsPressed/Released 生命周期。</summary>
    public PlayerInputFrame CaptureFrame()
    {
        _pressedScratch.Clear();
        _heldScratch.Clear();
        _releasedScratch.Clear();

        foreach (InputActionReference reference in _discreteInputs)
        {
            if (!InputBindingUtils.IsValid(reference))
                continue;

            InputAction action = reference.action;
            if (action.WasPressedThisFrame())
                _pressedScratch.Add(action.name);
            if (action.IsPressed())
                _heldScratch.Add(action.name);
            if (action.WasReleasedThisFrame())
                _releasedScratch.Add(action.name);
        }

        return new PlayerInputFrame(
            MoveInput,
            LookInput,
            _pressedScratch.ToArray(),
            _heldScratch.ToArray(),
            _releasedScratch.ToArray());
    }

    /// <summary>启用输入资产；由 PlayerController.OnEnable 调用。</summary>
    public void Enable() => inputActions.Enable();

    /// <summary>禁用输入资产；由 PlayerController.OnDisable 调用。</summary>
    public void Disable() => inputActions.Disable();

    /// <summary>解析 Player ActionMap；配置缺失视为 CharacterConfig 校验失败后的编程错误。</summary>
    void InitializeActions()
    {
        if (inputActions == null)
            throw new ArgumentNullException(nameof(inputActions), "InputReader: 未分配 InputActionAsset。");

        InputActionMap playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
    }
}
