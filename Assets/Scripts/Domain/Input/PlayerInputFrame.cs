using System;
using UnityEngine;

/// <summary>单帧玩家输入快照；可由设备、回放或网络注入。</summary>
public readonly struct PlayerInputFrame
{
    /// <summary>构造连续输入与离散输入生命周期快照。</summary>
    public PlayerInputFrame(
        Vector2 move,
        Vector2 look,
        string[] pressedInputIds,
        string[] heldInputIds,
        string[] releasedInputIds)
    {
        Move = move;
        Look = look;
        PressedInputIds = pressedInputIds ?? Array.Empty<string>();
        HeldInputIds = heldInputIds ?? Array.Empty<string>();
        ReleasedInputIds = releasedInputIds ?? Array.Empty<string>();
    }

    /// <summary>原始移动轴。</summary>
    public Vector2 Move { get; }
    /// <summary>原始视角轴。</summary>
    public Vector2 Look { get; }
    /// <summary>本帧按下的物理 Action 名。</summary>
    public string[] PressedInputIds { get; }
    /// <summary>当前保持按住的物理 Action 名。</summary>
    public string[] HeldInputIds { get; }
    /// <summary>本帧松开的物理 Action 名。</summary>
    public string[] ReleasedInputIds { get; }

    /// <summary>无连续量与离散状态的空帧。</summary>
    public static PlayerInputFrame Empty => new(
        Vector2.zero,
        Vector2.zero,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}
