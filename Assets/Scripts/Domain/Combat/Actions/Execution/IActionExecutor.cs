using System;

/// <summary>单角色动作执行器契约；Hitbox、VFX、Targeting 与 Character ActionState 共用。</summary>
public interface IActionExecutor
{
    bool IsPlaying { get; }

    ActionDefinition CurrentAction { get; }

    /// <summary>当前招式已播放秒数。</summary>
    float ElapsedSeconds { get; }

    /// <summary>当前招式逻辑帧（与 ActionDefinition.sampleRate 对齐）。</summary>
    int CurrentFrame { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    /// <summary>当前招式是否处于输入旋转修正窗口内。</summary>
    bool CanRotateByInput { get; }

    /// <summary>Logic Tick 帧推进事件；编辑器 Scrub 与 Play Mode 共用。</summary>
    event Action<CombatFrameContext> FrameAdvanced;

    /// <summary>直接播放已解析好的招式（起手 / Cancel / Transition 内部共用）。</summary>
    bool TryStart(ActionDefinition action);

    /// <summary>绑定 Cancel 窗口消费的离散输入缓冲桥接。</summary>
    void BindInputBuffer(IActionInputBuffer inputBuffer);

    /// <summary>绑定招式开始副作用上下文。</summary>
    void BindActionStartContext(IActionStartContext startContext);

    /// <summary>推进动作播放时间与逻辑帧。</summary>
    void Tick(float deltaTime);

    /// <summary>编辑器 Scrub 与 Play Mode 共用的 Logic Tick 入口。</summary>
    void UpdateFrame(int frameIndex);

    /// <summary>停止当前动作并清理播放状态。</summary>
    void Stop();
}
