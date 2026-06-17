public interface IActionRuntime
{
    bool IsPlaying { get; }

    bool TryStartDefaultAction();

    void BindComboInput(IActionComboInput comboInput);

    void Tick(float deltaTime);

    void Stop();
}
