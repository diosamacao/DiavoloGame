/// <summary>将 InputManager 缓冲暴露给招式运行时。</summary>
public sealed class InputManagerComboInput : IActionComboInput
{
    readonly InputManager _inputManager;

    public InputManagerComboInput(InputManager inputManager)
    {
        _inputManager = inputManager;
    }

    public bool HasBuffer(string inputId) => _inputManager.HasBuffer(inputId);

    public bool TryConsumeBuffer(string inputId) => _inputManager.TryConsumeBuffer(inputId);
}
