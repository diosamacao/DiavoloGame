/// <summary>取消窗口类型：显式连招拓扑，或退回 Locomotion。</summary>
public enum CancelType
{
    /// <summary>显式连招 Cancel：消费输入后解析 ActionGraph 显式边或共享路由。</summary>
    Combo = 0,

    /// <summary>移动 Cancel：有移动意图时退回 Locomotion，不走 Resolver。</summary>
    Movement = 1,
}

/// <summary>CancelType 扩展：是否属于会消费离散输入并走 Graph 的切招窗。</summary>
public static class CancelTypeExtensions
{
    /// <summary>Combo 窗消费输入并请求解析下一招；Recovery 能力由 Timeline Phase 窗口处理。</summary>
    public static bool ResolvesNextAction(this CancelType cancelType) =>
        cancelType == CancelType.Combo;
}
