using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>客户端进程级配置：只持有设备输入资产，不进入 Dedicated Gameplay Content。</summary>
public sealed class ClientRuntimeConfiguration
{
    const string InputActionsResourcesPath = "ACT/GameInputActions";

    ClientRuntimeConfiguration(InputActionAsset inputActions)
    {
        InputActions = inputActions;
    }

    /// <summary>客户端唯一设备输入资产；由 CombatWorldController 在启动时注入玩家座位。</summary>
    public InputActionAsset InputActions { get; }

    /// <summary>从唯一固定 Resources 路径加载并校验客户端配置；缺失时立即终止装配。</summary>
    public static ClientRuntimeConfiguration LoadRequired(UnityEngine.Object context)
    {
        InputActionAsset inputActions =
            Resources.Load<InputActionAsset>(InputActionsResourcesPath);
        if (inputActions == null)
        {
            throw new InvalidOperationException(
                "Client Runtime Configuration 缺少 "
                + $"'Assets/Resources/{InputActionsResourcesPath}.inputactions'。");
        }

        InputActionMap player = inputActions.FindActionMap("Player", false);
        if (player == null
            || player.FindAction("Move", false) == null
            || player.FindAction("Look", false) == null)
        {
            Debug.LogError(
                $"Client Runtime Configuration: '{inputActions.name}' 必须包含 Player/Move 与 Player/Look。",
                context);
            throw new InvalidOperationException("Client Runtime Configuration 输入映射无效。");
        }

        return new ClientRuntimeConfiguration(inputActions);
    }
}
