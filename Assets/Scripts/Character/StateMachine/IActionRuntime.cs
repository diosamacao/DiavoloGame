using System;

public interface IActionRuntime
{
    bool IsPlaying { get; }

    int AttackIndex { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    bool TryStartAttackChain();

    bool TryStartDodge();

    void BindComboInput(IActionComboInput comboInput);

    void BindDodgeFacing(Action onDodgeStarted);

    void Tick(float deltaTime);

    void Stop();
}
