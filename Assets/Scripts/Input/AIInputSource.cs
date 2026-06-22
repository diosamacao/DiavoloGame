using UnityEngine.InputSystem;

/// <summary>AI 输入源骨架：敌人 AI 可写入 PlayerInputFrame 后复用 CharacterActorFactory。</summary>
public sealed class AIInputSource : ICharacterInputSource
{
    PlayerInputFrame _frame = PlayerInputFrame.Empty;

    /// <summary>写入 AI 本帧决策结果；由敌人 AI / 行为树 / Utility AI 调用。</summary>
    public void SetFrame(PlayerInputFrame frame)
    {
        _frame = frame;
    }

    /// <summary>返回 AI 最近写入的一帧输入。</summary>
    public PlayerInputFrame CaptureFrame() => _frame;

    /// <summary>AI 不依赖 InputActionReference，忽略离散输入配置。</summary>
    public void ConfigureDiscreteInputs(InputActionReference[] references) { }

    /// <summary>AI 输入源无外部资源需要启用。</summary>
    public void Enable() { }

    /// <summary>AI 输入源无外部资源需要禁用。</summary>
    public void Disable() { }
}
