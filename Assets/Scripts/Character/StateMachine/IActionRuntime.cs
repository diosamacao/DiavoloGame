public interface IActionRuntime
{
    bool IsPlaying { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    bool TryStartDefaultAction();

    void BindComboInput(IActionComboInput comboInput);

    void Tick(float deltaTime);

    void Stop();
}
