using UnityEngine;

/// <summary>将移动意图解析为世界空间方向；PlayerController 等 Motor 层实现。</summary>
public interface IMoveIntentResolver
{
    /// <summary>Locomotion 默认转向平滑时间。</summary>
    float DefaultRotationSmoothTime { get; }

    /// <summary>将二维移动意图转为 XZ 平面单位方向；无意图时返回 zero。</summary>
    Vector3 ResolveWorldMoveDirection(Vector2 moveIntent);
}
