/// <summary>
/// 客机穿敌吸附 / 关碰撞窗内禁止走跑纠偏硬吸。
/// Branch_02/03 本机比权威卡肉更早穿过敌人，2m 阈会把人拽回敌前。
/// </summary>
public static class ActionMotionReconcileGate
{
    /// <summary>
    /// 本机或权威仍在吸附/关碰撞窗，或权威卡肉时，纠偏只 Ack 不改位姿。
    /// </summary>
    public static bool ShouldDeferLocomotionSnap(
        bool localSoftBodySuppressed,
        ActionDefinition localAction,
        int localActionFrame,
        in ActorReplicationSnapshot authority,
        ActionDefinition authorityAction)
    {
        if (localSoftBodySuppressed)
            return true;

        // 权威卡肉时位姿停在窗前/窗中；本机若已吸到背后，硬吸即拉回。
        if (authority.ActionId != 0 && authority.FreezeFrames > 0)
            return true;

        if (HasPassThroughWindow(localAction, localActionFrame))
            return true;

        return authority.ActionId != 0
            && HasPassThroughWindow(authorityAction, authority.ActionFrame);
    }

    /// <summary>应推迟硬吸时返回 <see cref="int.MaxValue"/>，否则 -1 走默认 2m 阈。</summary>
    public static int ResolveSnapThresholdMm(
        bool localSoftBodySuppressed,
        ActionDefinition localAction,
        int localActionFrame,
        in ActorReplicationSnapshot authority,
        ActionDefinition authorityAction) =>
        ShouldDeferLocomotionSnap(
            localSoftBodySuppressed,
            localAction,
            localActionFrame,
            in authority,
            authorityAction)
            ? int.MaxValue
            : -1;

    /// <summary>从本机 Actor 与权威快照解析纠偏阈；authorityAction 由 Catalog 预取。</summary>
    public static int ResolveSnapThresholdMm(
        CharacterActor actor,
        in ActorReplicationSnapshot authority,
        ActionDefinition authorityAction)
    {
        bool suppressed = actor?.MotorSim != null && actor.MotorSim.IsSoftBodySuppressed;
        ActionDefinition localAction = null;
        int localFrame = 0;
        if (actor?.ActionSim != null
            && actor.ActionSim.IsActive
            && actor.ActionSim.Snapshot.Content is ActionDefinition definition)
        {
            localAction = definition;
            localFrame = actor.ActionSim.Snapshot.CurrentFrame;
        }

        return ResolveSnapThresholdMm(
            suppressed,
            localAction,
            localFrame,
            in authority,
            authorityAction);
    }

    /// <summary>指定招的当前帧是否有 TargetAdhesion 或 SoftBodySuppress。</summary>
    public static bool HasPassThroughWindow(ActionDefinition action, int frame) =>
        action != null && action.Timeline.HasAdhesionOrSoftBodySuppressAtFrame(frame);
}
