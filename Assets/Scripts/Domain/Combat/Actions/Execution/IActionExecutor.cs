using System;

/// <summary>单角色动作执行器契约；Hitbox、VFX、Targeting 与 Character ActionState 共用。</summary>
public interface IActionExecutor
{
    bool IsPlaying { get; }

    ActionDefinition CurrentAction { get; }

    /// <summary>当前权威动作帧；可等于 TotalFrames 表示完整时长结束。</summary>
    int CurrentFrame { get; }

    /// <summary>当前动作会话的稳定编号；无动作时为 0。</summary>
    int CurrentActionInstanceId { get; }

    /// <summary>当前招式是否处于可移动取消的帧窗口内。</summary>
    bool CanCancelByMovement { get; }

    /// <summary>当前招式是否处于输入旋转修正窗口内。</summary>
    bool CanRotateByInput { get; }

    /// <summary>Runtime 整数动作帧推进事件。</summary>
    event Action<CombatFrameContext> FrameAdvanced;

    /// <summary>直接播放已解析好的招式（起手 / Cancel / Transition 内部共用）。</summary>
    bool TryStart(ActionDefinition action);

    /// <summary>
    /// 高优硬打断：候选招优先级严格大于当前招且当前帧可打断时，强制切到已解析结果。
    /// </summary>
    bool TryInterrupt(in ActionResolveResult resolveResult);

    /// <summary>绑定 Cancel 窗口消费的离散输入缓冲桥接。</summary>
    void BindInputBuffer(IActionInputBuffer inputBuffer);

    /// <summary>绑定 ActionGraph 节点起手行为使用的上下文。</summary>
    void BindActionStartContext(IActionStartContext startContext);

    /// <summary>每个 SimulationWorld 逻辑帧调用一次，推进整数动作帧与窗口。</summary>
    void Step(float fixedDeltaSeconds);

    /// <summary>指定动作会话是否已经结束或切换，用于状态机按逻辑结果收尾。</summary>
    bool HasEndedActionInstance(int instanceId);

    /// <summary>停止当前动作并清理播放状态。</summary>
    void Stop();
}
