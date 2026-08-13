using UnityEngine;

/// <summary>
/// 移动欲望帧槽；控制方写入，角色移动系统通过 IMoveIntentSource 只读消费。
/// </summary>
public sealed class LocomotionDesireBuffer : IMoveIntentSource
{
    const float MoveIntentThresholdSq = 0.01f;

    LocomotionDesire _pending;
    Vector2 _bufferedMoveIntent;

    /// <summary>当前欲望（调试 / 控制方读取）。</summary>
    public LocomotionDesire Pending => _pending;

    /// <inheritdoc />
    public Vector2 MoveIntent => _pending.LocalMove;

    /// <inheritdoc />
    public float MoveMagnitude => Mathf.Clamp01(_pending.LocalMove.magnitude);

    /// <inheritdoc />
    public bool HasMoveIntent => _pending.LocalMove.sqrMagnitude >= MoveIntentThresholdSq;

    /// <inheritdoc />
    public Vector2 BufferedMoveIntent => _bufferedMoveIntent;

    /// <inheritdoc />
    public ushort MoveReferenceYawQuantized => _pending.MoveReferenceYawQuantized;

    /// <summary>提交本帧欲望；非零方向同时更新最近有效方向。</summary>
    public void Set(in LocomotionDesire desire)
    {
        _pending = desire;
        if (desire.LocalMove.sqrMagnitude >= MoveIntentThresholdSq)
            _bufferedMoveIntent = desire.LocalMove;
    }

    /// <summary>读取当前欲望。</summary>
    public bool TryPeek(out LocomotionDesire desire)
    {
        desire = _pending;
        return true;
    }

    /// <summary>清空当前欲望为停步；保留最近有效方向。</summary>
    public void Clear() => _pending = LocomotionDesire.None;

    /// <summary>清空最近有效方向。</summary>
    public void ClearBufferedMoveIntent() => _bufferedMoveIntent = Vector2.zero;
}
