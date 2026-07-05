using UnityEngine;

/// <summary>招式开始时的副作用执行上下文（由 PlayerController 等 Motor 层实现并注入）。</summary>
public interface IActionStartContext
{
    /// <summary>读取 Dodge 判定方向（优先当前输入，其次缓冲输入）。</summary>
    bool TryGetDodgeIntentDirection(out Vector3 direction);

    /// <summary>按给定世界方向立即对齐朝向（忽略 y 分量）。</summary>
    void FaceWorldDirection(Vector3 direction);

    /// <summary>按当前或缓冲移动输入朝向，用于通用起手朝向修正。</summary>
    void FaceBufferedMoveIntent();
}
