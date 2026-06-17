public interface IActionRuntime
{
    bool IsPlaying { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    bool TryStartDefaultAction();

    bool TryStartDefaultDodge();

    void BindComboInput(IActionComboInput comboInput);

    void Tick(float deltaTime);

    void Stop();
}
