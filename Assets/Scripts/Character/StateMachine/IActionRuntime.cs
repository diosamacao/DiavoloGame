using System;

public interface IActionRuntime
{
    bool IsPlaying { get; }

    ActionDefinition CurrentAction { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    /// <summary>按出招表入口 id 从 Locomotion 起手。</summary>
    bool TryStartByInput(string inputId);

    /// <summary>直接播放指定招式（ComboLink / Transition 内部使用）。</summary>
    bool TryStart(ActionDefinition action);

    void BindComboInput(IActionComboInput comboInput);

    void BindActionStartContext(IActionStartContext startContext);

    void Tick(float deltaTime);

    void Stop();
}
