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

    readonly Dictionary<InputSlot, Action> _pressedHandlers = new();
    readonly HashSet<InputSlot> _buffer = new();

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

    public void RegisterPressed(InputSlot slot, Action handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _pressedHandlers[slot] = handler;
    }

    public void UnregisterPressed(InputSlot slot) => _pressedHandlers.Remove(slot);

    /// <summary>摄入一帧输入；回放/网络可直接构造 PlayerInputFrame 调用，无需 IPlayerInputSource。</summary>
    public void IngestFrame(PlayerInputFrame frame)
    {
        _frame = frame;
        UpdateMoveBuffer(frame.Move);

        if (frame.AttackPressed)
            NotifyPressed(InputSlot.Attack);

        if (frame.DodgePressed)
            NotifyPressed(InputSlot.Dodge);
    }

    public void NotifyPressed(InputSlot slot)
    {
        if (_pressedHandlers.TryGetValue(slot, out Action handler))
            handler.Invoke();
    }

    public void Buffer(InputSlot slot) => _buffer.Add(slot);

    public bool HasBuffer(InputSlot slot) => _buffer.Contains(slot);

    public bool TryConsumeBuffer(InputSlot slot) => _buffer.Remove(slot);

    public void ClearBuffer(InputSlot slot) => _buffer.Remove(slot);

    public void ClearAllBuffers() => _buffer.Clear();

    public void ClearBufferedMoveIntent() => _bufferedMoveIntent = Vector2.zero;

    void UpdateMoveBuffer(Vector2 move)
    {
        if (move.sqrMagnitude >= MoveIntentThresholdSq)
            _bufferedMoveIntent = move;
    }
}
