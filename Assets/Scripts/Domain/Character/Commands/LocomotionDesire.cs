using UnityEngine;

/// <summary>角色一帧移动欲望；用于 AI、脚本控制与未来回放，不携带具体控制方概念。</summary>
public readonly struct LocomotionDesire
{
    /// <summary>停步 / 空欲望。</summary>
    public static LocomotionDesire None => default;

    /// <summary>创建本地平面移动欲望；localMove 会钳到单位圆内，referenceYaw 决定其世界参考。</summary>
    public LocomotionDesire(Vector2 localMove, bool faceTarget, float referenceYawDegrees)
    {
        LocalMove = Vector2.ClampMagnitude(localMove, 1f);
        FaceTarget = faceTarget;
        MoveReferenceYawQuantized = InputQuantizer.QuantizeYaw(referenceYawDegrees);
    }

    /// <summary>本地移动轴（x 侧移、y 前进）。</summary>
    public Vector2 LocalMove { get; }

    /// <summary>控制方是否请求朝向目标。</summary>
    public bool FaceTarget { get; }

    /// <summary>本地移动轴对应的世界参考偏航。</summary>
    public ushort MoveReferenceYawQuantized { get; }

    /// <summary>是否有有效移动分量。</summary>
    public bool HasMove => LocalMove.sqrMagnitude >= 0.01f;
}
