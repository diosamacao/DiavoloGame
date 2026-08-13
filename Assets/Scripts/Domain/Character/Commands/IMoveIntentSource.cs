using UnityEngine;

/// <summary>角色移动意图只读源；玩家输入、AI 命令与回放可提供不同实现。</summary>
public interface IMoveIntentSource
{
    /// <summary>本帧本地移动轴（x 侧移、y 前进）。</summary>
    Vector2 MoveIntent { get; }

    /// <summary>本帧移动幅度，范围 0–1。</summary>
    float MoveMagnitude { get; }

    /// <summary>本帧是否存在有效移动意图。</summary>
    bool HasMoveIntent { get; }

    /// <summary>最近一次有效移动方向，供起手朝向等上下文行为使用。</summary>
    Vector2 BufferedMoveIntent { get; }

    /// <summary>把本地移动轴旋转到世界平面的量化参考偏航。</summary>
    ushort MoveReferenceYawQuantized { get; }
}
