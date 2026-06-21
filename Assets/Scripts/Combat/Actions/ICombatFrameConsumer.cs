/// <summary>订阅 ActionRuntimeController Logic Tick 的子系统（Hitbox、VFX、未来 ActionEvent 等）。</summary>
public interface ICombatFrameConsumer
{
    /// <summary>新招式开始播放时调用，用于清空帧追踪状态。</summary>
    void OnActionBegan(ActionDefinition action);

    /// <summary>逻辑帧推进时调用；编辑器 Scrub 与 Play Mode 走同一路径。</summary>
    void OnCombatFrameAdvanced(in CombatFrameContext context);

    /// <summary>招式停止或切换前调用。</summary>
    void OnActionEnded();
}
