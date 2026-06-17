/// <summary>将 InputManager 的 Attack 缓冲暴露给招式运行时。</summary>
public sealed class InputManagerComboInput : IActionComboInput
{
    readonly InputManager _inputManager;

    public InputManagerComboInput(InputManager inputManager)
    {
        _inputManager = inputManager;
    }

    public bool HasBufferedAttack => _inputManager.HasBuffer(InputSlot.Attack);

    public void ConsumeBufferedAttack() => _inputManager.TryConsumeBuffer(InputSlot.Attack);
}
