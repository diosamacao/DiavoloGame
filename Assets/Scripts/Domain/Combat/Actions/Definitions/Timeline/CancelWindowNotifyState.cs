using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>取消窗口区间；Action/Recovery 取消消费输入走 Resolver，Movement 取消允许退回移动。</summary>
[Serializable]
public class CancelWindowNotifyState : ActionNotifyState
{
    [SerializeField] CancelType cancelType = CancelType.Action;
    [Tooltip("Action / Recovery 时选择允许的输入；Movement 时忽略。")]
    [SerializeField] InputActionReference[] allowedInputs = Array.Empty<InputActionReference>();

    /// <summary>窗口取消类型：连招进位、后摇重开或移动取消。</summary>
    public CancelType CancelType => cancelType;

    /// <summary>解析后的输入 id 列表（Input System Action 名）。</summary>
    public string[] AllowedInputs => InputBindingUtils.ResolveInputIds(allowedInputs);

    /// <summary>转为运行时只读窗口，避免每帧直接触碰 InputActionReference。</summary>
    public ResolvedCancelWindow ToResolved() =>
        new(StartFrame, EndFrame, cancelType, AllowedInputs, Priority);
}
