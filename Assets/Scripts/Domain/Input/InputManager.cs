using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家原始输入中枢：摄入每帧快照，提供连续输入与离散输入生命周期。
/// 设备输入到玩法语义的转换由 GameplayIntentProducer 负责。
/// </summary>
public sealed class InputManager
{
    const float MoveIntentThresholdSq = 0.01f;

    readonly HashSet<string> _pressedInputIds = new(System.StringComparer.Ordinal);
    readonly HashSet<string> _heldInputIds = new(System.StringComparer.Ordinal);
    readonly HashSet<string> _releasedInputIds = new(System.StringComparer.Ordinal);

    PlayerInputFrame _frame = PlayerInputFrame.Empty;
    Vector2 _bufferedMoveIntent;

    /// <summary>本帧移动意图（摇杆/WASD 原始值）。</summary>
    public Vector2 MoveIntent => _frame.Move;

    /// <summary>本帧视角意图。</summary>
    public Vector2 LookIntent => _frame.Look;

    /// <summary>最近一帧非零移动意图；用于招式中预输入闪避方向等。</summary>
    public Vector2 BufferedMoveIntent => _bufferedMoveIntent;

    /// <summary>当前移动轴幅度，限制为 0–1。</summary>
    public float MoveMagnitude => Mathf.Clamp01(_frame.Move.magnitude);

    /// <summary>当前帧是否存在有效移动意图。</summary>
    public bool HasMoveIntent => _frame.Move.sqrMagnitude >= MoveIntentThresholdSq;

    /// <summary>是否记录过最近一次有效移动意图。</summary>
    public bool HasBufferedMoveIntent => _bufferedMoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    /// <summary>摄入一帧输入；回放/网络可直接构造 PlayerInputFrame 调用，无需 ICharacterInputSource。</summary>
    public void IngestFrame(PlayerInputFrame frame)
    {
        _frame = frame;
        UpdateMoveBuffer(frame.Move);
        ReplaceSet(_pressedInputIds, frame.PressedInputIds);
        ReplaceSet(_heldInputIds, frame.HeldInputIds);
        ReplaceSet(_releasedInputIds, frame.ReleasedInputIds);
    }

    /// <summary>指定物理输入是否在本帧按下。</summary>
    public bool WasPressedThisFrame(string inputId) =>
        !string.IsNullOrEmpty(inputId) && _pressedInputIds.Contains(inputId);

    /// <summary>指定物理输入当前是否保持按住。</summary>
    public bool IsPressed(string inputId) =>
        !string.IsNullOrEmpty(inputId) && _heldInputIds.Contains(inputId);

    /// <summary>指定物理输入是否在本帧松开。</summary>
    public bool WasReleasedThisFrame(string inputId) =>
        !string.IsNullOrEmpty(inputId) && _releasedInputIds.Contains(inputId);

    /// <summary>清除最近一次有效移动方向。</summary>
    public void ClearBufferedMoveIntent() => _bufferedMoveIntent = Vector2.zero;

    void UpdateMoveBuffer(Vector2 move)
    {
        if (move.sqrMagnitude >= MoveIntentThresholdSq)
            _bufferedMoveIntent = move;
    }

    /// <summary>以本帧快照替换离散状态集合，避免上一帧状态残留。</summary>
    static void ReplaceSet(HashSet<string> target, string[] source)
    {
        target.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            if (!string.IsNullOrEmpty(source[i]))
                target.Add(source[i]);
        }
    }
}
