using UnityEngine;

/// <summary>单帧 Locomotion 输入与运动学快照。</summary>
public readonly struct LocomotionInputSnapshot
{
    public LocomotionInputSnapshot(
        Vector2 moveIntent,
        float magnitude,
        bool hasMoveInput,
        Vector3 worldMoveDirection,
        bool isGrounded,
        float planarSpeed)
    {
        MoveIntent = moveIntent;
        Magnitude = magnitude;
        HasMoveInput = hasMoveInput;
        WorldMoveDirection = worldMoveDirection;
        IsGrounded = isGrounded;
        PlanarSpeed = planarSpeed;
    }

    public Vector2 MoveIntent { get; }
    public float Magnitude { get; }
    public bool HasMoveInput { get; }
    public Vector3 WorldMoveDirection { get; }
    public bool IsGrounded { get; }

    /// <summary>上一帧水平位移估算速度（m/s），供 Gait→Stop 门槛使用。</summary>
    public float PlanarSpeed { get; }
}
