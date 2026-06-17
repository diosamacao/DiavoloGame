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
