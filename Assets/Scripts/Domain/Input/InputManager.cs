using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家输入中枢：摄入每帧快照，提供移动/视角意图与离散按键缓冲。
/// 玩法层只读意图；位移执行由 PlayerController 等消费者负责。
/// </summary>
public sealed class InputManager
{
    const float MoveIntentThresholdSq = 0.01f;

    readonly Dictionary<string, Action> _pressedHandlers = new(StringComparer.Ordinal);
    readonly HashSet<string> _buffer = new(StringComparer.Ordinal);

    PlayerInputFrame _frame = PlayerInputFrame.Empty;
    Vector2 _bufferedMoveIntent;

    /// <summary>本帧移动意图（摇杆/WASD 原始值）。</summary>
    public Vector2 MoveIntent => _frame.Move;

    /// <summary>本帧视角意图。</summary>
    public Vector2 LookIntent => _frame.Look;

    /// <summary>最近一帧非零移动意图；用于招式中预输入闪避方向等。</summary>
    public Vector2 BufferedMoveIntent => _bufferedMoveIntent;

    public float MoveMagnitude => Mathf.Clamp01(_frame.Move.magnitude);

    public bool HasMoveIntent => _frame.Move.sqrMagnitude >= MoveIntentThresholdSq;

    public bool HasBufferedMoveIntent => _bufferedMoveIntent.sqrMagnitude >= MoveIntentThresholdSq;

    public void RegisterPressed(string inputId, Action handler)
    {
        if (string.IsNullOrEmpty(inputId))
            throw new ArgumentException("inputId 不能为空。", nameof(inputId));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _pressedHandlers[inputId] = handler;
    }

    public void UnregisterPressed(string inputId) => _pressedHandlers.Remove(inputId);

    /// <summary>摄入一帧输入；回放/网络可直接构造 PlayerInputFrame 调用，无需 ICharacterInputSource。</summary>
    public void IngestFrame(PlayerInputFrame frame)
    {
        _frame = frame;
        UpdateMoveBuffer(frame.Move);

        foreach (string inputId in frame.PressedInputIds)
            NotifyPressed(inputId);
    }

    public void NotifyPressed(string inputId)
    {
        if (string.IsNullOrEmpty(inputId))
            return;

        if (_pressedHandlers.TryGetValue(inputId, out Action handler))
            handler.Invoke();
    }

    public void Buffer(string inputId)
    {
        if (!string.IsNullOrEmpty(inputId))
            _buffer.Add(inputId);
    }

    public bool HasBuffer(string inputId) =>
        !string.IsNullOrEmpty(inputId) && _buffer.Contains(inputId);

    public bool TryConsumeBuffer(string inputId) =>
        !string.IsNullOrEmpty(inputId) && _buffer.Remove(inputId);

    public void ClearBuffer(string inputId) => _buffer.Remove(inputId);

    public void ClearAllBuffers() => _buffer.Clear();

    public void ClearBufferedMoveIntent() => _bufferedMoveIntent = Vector2.zero;

    void UpdateMoveBuffer(Vector2 move)
    {
        if (move.sqrMagnitude >= MoveIntentThresholdSq)
            _bufferedMoveIntent = move;
    }
}
