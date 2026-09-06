/// <summary>弹刀卡肉帧数只认玩家当前招架窗；真伤仍走进攻盒 UseHitStop。</summary>
public static class AssistParryHitStop
{
    /// <summary>窗未配置或找不到窗时的回退逻辑帧（60Hz）。</summary>
    public const int DefaultFrames = 8;

    /// <summary>读窗上的帧；空窗回退 <see cref="DefaultFrames"/>。</summary>
    public static int ResolveFrames(AssistParryWindowNotifyState window) =>
        window != null ? window.HitStopFrames : DefaultFrames;

    /// <summary>读指定招在该帧生效的招架窗；无窗回退默认帧。</summary>
    public static int ResolveFrames(ActionDefinition action, int frame)
    {
        if (action != null && action.TryGetAssistParryWindowAtFrame(frame, out AssistParryWindowNotifyState window))
            return ResolveFrames(window);

        return DefaultFrames;
    }

    /// <summary>读玩家当前 ActionSim 招架窗。无活动招或无窗时回退默认帧。</summary>
    public static int ResolveFrames(CharacterActor player)
    {
        if (player?.ActionSim == null || !player.ActionSim.IsActive)
            return DefaultFrames;

        ActionSimSnapshot snap = player.ActionSim.Snapshot;
        return snap.Content is ActionDefinition action
            ? ResolveFrames(action, snap.CurrentFrame)
            : DefaultFrames;
    }
}
