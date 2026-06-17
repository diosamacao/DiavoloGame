public interface IActionRuntime
{
    bool IsPlaying { get; }

    bool TryStartDefaultAction();

    void BufferAttackInput();

    void Tick(float deltaTime);

    void Stop();
}
