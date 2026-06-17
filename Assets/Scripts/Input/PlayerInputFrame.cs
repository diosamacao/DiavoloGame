using UnityEngine;

/// <summary>单帧玩家输入快照；可由设备、回放或网络注入。</summary>
public readonly struct PlayerInputFrame
{
    public PlayerInputFrame(Vector2 move, Vector2 look, bool attackPressed, bool dodgePressed)
    {
        Move = move;
        Look = look;
        AttackPressed = attackPressed;
        DodgePressed = dodgePressed;
    }

    public Vector2 Move { get; }
    public Vector2 Look { get; }
    public bool AttackPressed { get; }
    public bool DodgePressed { get; }

    public static PlayerInputFrame Empty => new(Vector2.zero, Vector2.zero, false, false);
}
