public interface IActionRuntime
{
    bool IsPlaying { get; }

    bool TryStartDefaultAction();

    void Tick(float deltaTime);

    void Stop();
}
