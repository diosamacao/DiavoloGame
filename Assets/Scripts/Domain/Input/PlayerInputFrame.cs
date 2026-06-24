using System;
using UnityEngine;

/// <summary>单帧玩家输入快照；可由设备、回放或网络注入。</summary>
public readonly struct PlayerInputFrame
{
    public PlayerInputFrame(Vector2 move, Vector2 look, string[] pressedInputIds)
    {
        Move = move;
        Look = look;
        PressedInputIds = pressedInputIds ?? Array.Empty<string>();
    }

    public Vector2 Move { get; }
    public Vector2 Look { get; }
    public string[] PressedInputIds { get; }

    public static PlayerInputFrame Empty => new(Vector2.zero, Vector2.zero, Array.Empty<string>());
}
