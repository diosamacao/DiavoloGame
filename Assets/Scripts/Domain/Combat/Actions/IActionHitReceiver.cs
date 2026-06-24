/// <summary>招式运行时命中回流：由 HitBoxSystem 调用，支撑 OnHitConfirm Transition。</summary>
public interface IActionHitReceiver
{
    /// <summary>本招是否已至少命中一次（用于 Transition 与编辑器调试）。</summary>
    bool HasConfirmedHitThisAction { get; }

    /// <summary>记录一次命中；同一招式可多次调用（HitStop 等仍按 ActionDefinition 配置）。</summary>
    void NotifyHit(in ActionHitContext context);
}
