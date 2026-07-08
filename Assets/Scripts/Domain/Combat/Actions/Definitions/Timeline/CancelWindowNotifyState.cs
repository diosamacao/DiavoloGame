using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>取消窗口区间；Action 取消消费输入，Movement 取消允许退回移动。</summary>
[Serializable]
public class CancelWindowNotifyState : ActionNotifyState
{
    [SerializeField] CancelType cancelType = CancelType.Action;
    [Tooltip("CancelType.Action 时从 GameInputActions 选择允许的 Action；Movement 时忽略。")]
    [SerializeField] InputActionReference[] allowedInputs = Array.Empty<InputActionReference>();

    /// <summary>窗口取消类型：切招或移动取消。</summary>
    public CancelType CancelType => cancelType;

    /// <summary>解析后的输入 id 列表（Input System Action 名）。</summary>
    public string[] AllowedInputs => InputBindingUtils.ResolveInputIds(allowedInputs);

    /// <summary>转为运行时只读窗口，避免每帧直接触碰 InputActionReference。</summary>
    public ResolvedCancelWindow ToResolved() =>
        new(StartFrame, EndFrame, cancelType, AllowedInputs, Priority);
}
