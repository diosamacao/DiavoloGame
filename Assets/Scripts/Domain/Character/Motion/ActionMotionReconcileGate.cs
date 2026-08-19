/// <summary>
/// 客机穿敌吸附 / 关碰撞 / 闪避 / 烘焙大位移进行中禁止走跑纠偏硬吸。
/// 本机已到敌后或连闪落点、延迟快照还停在旧位时，2m 阈会把人拽回。
/// </summary>
public static class ActionMotionReconcileGate
{
    /// <summary>
    /// 本机或权威仍在修正位移、权威卡肉时，纠偏只 Ack 不改位姿。
    /// 吸附/关碰撞看整段招，不看当前帧是否刚好落在窗口里。
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

        if (HasCorrectiveDisplacement(localAction))
            return true;

        if (authority.ActionId != 0 && HasCorrectiveDisplacement(authorityAction))
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
        TryReadLocalAction(actor, out ActionDefinition localAction, out int localFrame);
        bool suppressed = actor?.MotorSim != null && actor.MotorSim.IsSoftBodySuppressed;
        return ResolveSnapThresholdMm(
            suppressed,
            localAction,
            localFrame,
            in authority,
            authorityAction);
    }

    /// <summary>读取本机当前招；空闲时 action 为 null。</summary>
    public static bool TryReadLocalAction(
        CharacterActor actor,
        out ActionDefinition action,
        out int actionFrame)
    {
        action = null;
        actionFrame = 0;
        if (actor?.ActionSim == null
            || !actor.ActionSim.IsActive
            || actor.ActionSim.Snapshot.Content is not ActionDefinition definition)
        {
            return false;
        }

        action = definition;
        actionFrame = actor.ActionSim.Snapshot.CurrentFrame;
        return true;
    }

    /// <summary>
    /// 闪避、吸附/关碰撞窗、或烘焙位移招：中途误差不当走跑分叉。
    /// </summary>
    public static bool HasCorrectiveDisplacement(ActionDefinition action)
    {
        if (action == null)
            return false;
        if (action.ActionType == CombatActionType.Dodge)
            return true;
        if (action.Timeline.HasAdhesionOrSoftBodySuppressWindow())
            return true;
        return action.ExecutionPolicy.BaseMotionMode == ActionBaseMotionMode.BakedMotion;
    }

    /// <summary>指定招的当前帧是否有 TargetAdhesion 或 SoftBodySuppress。</summary>
    public static bool HasPassThroughWindow(ActionDefinition action, int frame) =>
        action != null && action.Timeline.HasAdhesionOrSoftBodySuppressAtFrame(frame);
}
