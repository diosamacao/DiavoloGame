using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>从 Input System 采集设备状态，并在边界处量化为 InputFrame。</summary>
public sealed class InputReader : ILocalInputSampler
{
    InputActionAsset inputActions = null!;
    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputActionReference[] _discreteInputs = System.Array.Empty<InputActionReference>();

    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

    /// <summary>创建纯 C# 输入源；由 PlayerController 按 CharacterConfig 构造。</summary>
    public InputReader(InputActionAsset actions)
    {
        inputActions = actions;
        InitializeActions();
    }

    /// <summary>由工厂根据全局 GameplayIntentProfile 注入离散 Action，无需在 Prefab 重复配置。</summary>
    public void ConfigureDiscreteInputs(InputActionReference[] references)
    {
        _discreteInputs = references ?? Array.Empty<InputActionReference>();
    }

    /// <summary>采集连续轴与离散生命周期，并直接写成固定 bit 与量化轴。</summary>
    public InputFrame Sample(long targetFrame, SimActorId actorId)
    {
        ulong pressed = 0ul;
        ulong held = 0ul;
        ulong released = 0ul;

        foreach (InputActionReference reference in _discreteInputs)
        {
            if (!InputBindingUtils.TryGetButton(reference, out InputButton button))
                continue;

            InputAction action = reference.action;
            ulong mask = InputButtonMask.Of(button);
            if (action.WasPressedThisFrame())
                pressed |= mask;
            if (action.IsPressed())
                held |= mask;
            if (action.WasReleasedThisFrame())
                released |= mask;
        }

        Vector2 move = MoveInput;
        return new InputFrame(
            targetFrame,
            actorId,
            InputQuantizer.QuantizeAxis(move.x),
            InputQuantizer.QuantizeAxis(move.y),
            pressed,
            held,
            released);
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
