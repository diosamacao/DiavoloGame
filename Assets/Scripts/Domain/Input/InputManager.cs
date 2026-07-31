using UnityEngine;

/// <summary>
/// 角色量化输入中枢：摄入每逻辑帧快照，提供连续输入与离散输入生命周期。
/// 设备输入到玩法语义的转换由 GameplayIntentProducer 负责。
/// </summary>
public sealed class InputManager
{
    const float MoveIntentThresholdSq = 0.01f;

    InputFrame _frame;
    Vector2 _bufferedMoveIntent;

    /// <summary>本帧量化移动轴反解值；仅供现有 Motor 过渡使用。</summary>
    public Vector2 MoveIntent => new(
        InputQuantizer.DequantizeAxis(_frame.MoveX),
        InputQuantizer.DequantizeAxis(_frame.MoveY));

    /// <summary>最近一帧非零移动意图；用于招式中预输入闪避方向等。</summary>
    public Vector2 BufferedMoveIntent => _bufferedMoveIntent;

    /// <summary>当前移动轴幅度，限制为 0–1。</summary>
    public float MoveMagnitude => Mathf.Clamp01(MoveIntent.magnitude);

    /// <summary>当前帧是否存在有效移动意图。</summary>
    public bool HasMoveIntent => MoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    /// <summary>是否记录过最近一次有效移动意图。</summary>
    public bool HasBufferedMoveIntent => _bufferedMoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    /// <summary>摄入一帧量化输入；设备、AI、回放与网络都必须走同一格式。</summary>
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
