using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>从 Input System 采集设备状态，并在边界处量化为 InputFrame。</summary>
public sealed class InputReader : ILocalInputSampler
{
    InputActionAsset inputActions = null!;
    InputAction moveAction = null!;
    InputAction lookAction = null!;
    InputAction cameraLockAction;
    InputAction targetSwitchLeftAction;
    InputAction targetSwitchRightAction;
    InputAction switchCharacterAction;
    InputActionReference[] _discreteInputs = System.Array.Empty<InputActionReference>();
    ushort _stagedMoveReferenceYaw;

    /// <inheritdoc />
    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

    /// <inheritdoc />
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

    /// <inheritdoc />
    public bool HasMoveIntent => MoveInput.sqrMagnitude >= InputManager.MoveIntentThresholdSq;

    /// <inheritdoc />
    public bool CameraLockPressedThisFrame =>
        cameraLockAction != null && cameraLockAction.WasPressedThisFrame();

    /// <inheritdoc />
    public void StageMoveReferenceYaw(float yawDegrees) =>
        _stagedMoveReferenceYaw = InputQuantizer.QuantizeYaw(yawDegrees);

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

        SampleOptionalButton(targetSwitchLeftAction, InputButton.TargetSwitchLeft, ref pressed, ref held, ref released);
        SampleOptionalButton(targetSwitchRightAction, InputButton.TargetSwitchRight, ref pressed, ref held, ref released);
        // 阵容切人不进入 GameplayIntentProfile，由座位级 Party 协调器消费。
        SampleOptionalButton(switchCharacterAction, InputButton.SwitchCharacter, ref pressed, ref held, ref released);

        Vector2 move = MoveInput;
        return new InputFrame(
            targetFrame,
            actorId,
            InputQuantizer.QuantizeAxis(move.x),
            InputQuantizer.QuantizeAxis(move.y),
            pressed,
            held,
            released,
            _stagedMoveReferenceYaw);
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
        cameraLockAction = playerMap.FindAction("CameraLock", throwIfNotFound: false);
        targetSwitchLeftAction = playerMap.FindAction("TargetSwitchLeft", throwIfNotFound: false);
        targetSwitchRightAction = playerMap.FindAction("TargetSwitchRight", throwIfNotFound: false);
        switchCharacterAction = playerMap.FindAction("SwitchCharacter", throwIfNotFound: false);
    }

    /// <summary>把可选 Player Action 采样到稳定按钮位；资产未配置时保持未按下。</summary>
    static void SampleOptionalButton(
        InputAction action,
        InputButton button,
        ref ulong pressed,
        ref ulong held,
        ref ulong released)
    {
        if (action == null)
            return;

        ulong mask = InputButtonMask.Of(button);
        if (action.WasPressedThisFrame())
            pressed |= mask;
        if (action.IsPressed())
            held |= mask;
        if (action.WasReleasedThisFrame())
            released |= mask;
    }
}
