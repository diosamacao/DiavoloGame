using System;
using System.Collections.Generic;

public sealed class InputManager
{
    readonly Dictionary<InputSlot, Action> _pressedHandlers = new();
    readonly HashSet<InputSlot> _buffer = new();

    public void RegisterPressed(InputSlot slot, Action handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _pressedHandlers[slot] = handler;
    }

    public void UnregisterPressed(InputSlot slot) => _pressedHandlers.Remove(slot);

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
}
