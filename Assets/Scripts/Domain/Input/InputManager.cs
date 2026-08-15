using UnityEngine;

/// <summary>
/// 角色量化输入中枢：摄入每逻辑帧快照，提供连续输入与离散输入生命周期。
/// 设备输入到玩法语义的转换由 GameplayIntentProducer 负责。
/// </summary>
public sealed class InputManager : IMoveIntentSource
{
    /// <summary>与设备采样共用的移动死区（平方）。</summary>
    public const float MoveIntentThresholdSq = 0.01f;

    InputFrame _frame;
    Vector2 _bufferedMoveIntent;

    /// <inheritdoc />
    public Vector2 MoveIntent => new(
        InputQuantizer.DequantizeAxis(_frame.MoveX),
        InputQuantizer.DequantizeAxis(_frame.MoveY));

    /// <inheritdoc />
    public Vector2 BufferedMoveIntent => _bufferedMoveIntent;

    /// <inheritdoc />
    public float MoveMagnitude => Mathf.Clamp01(MoveIntent.magnitude);

    /// <inheritdoc />
    public bool HasMoveIntent => MoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    /// <inheritdoc />
    public ushort MoveReferenceYawQuantized => _frame.MoveReferenceYawQuantized;

    /// <summary>是否记录过最近一次有效移动意图。</summary>
    public bool HasBufferedMoveIntent => _bufferedMoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    /// <summary>摄入一帧量化输入并刷新玩家移动方向缓冲。</summary>
    public void IngestFrame(InputFrame frame)
    {
        _frame = frame;
        UpdateMoveBuffer(MoveIntent);
    }

    /// <summary>指定稳定按钮是否在本帧按下。</summary>
    public bool WasPressedThisFrame(InputButton button) => _frame.WasPressed(button);

    /// <summary>指定稳定按钮当前是否保持按住。</summary>
    public bool IsPressed(InputButton button) => _frame.IsHeld(button);

    /// <summary>指定稳定按钮是否在本帧松开。</summary>
    public bool WasReleasedThisFrame(InputButton button) => _frame.WasReleased(button);

    /// <summary>清除最近一次有效移动方向。</summary>
    public void ClearBufferedMoveIntent() => _bufferedMoveIntent = Vector2.zero;

    void UpdateMoveBuffer(Vector2 move)
    {
        if (move.sqrMagnitude >= MoveIntentThresholdSq)
            _bufferedMoveIntent = move;
    }

}
