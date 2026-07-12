/// <summary>取消窗口类型：连招进位、后摇重开，或退回 Locomotion。</summary>
public enum CancelType
{
    /// <summary>连招 Cancel：消费输入后由 Resolver 进位到下一步（队列）或查树边。</summary>
    Action = 0,

    /// <summary>移动 Cancel：有移动意图时退回 Locomotion，不走 Resolver。</summary>
    Movement = 1,

    /// <summary>后摇 Cancel：消费输入后由 ComboResolver 回到连招首段（steps[0]），不进位。</summary>
    Recovery = 2,
}

/// <summary>CancelType 扩展：是否属于会消费离散输入并走 Resolver 的切招窗。</summary>
public static class CancelTypeExtensions
{
    /// <summary>Action / Recovery 窗都会消费输入并请求解析下一招。</summary>
    public static bool ResolvesNextAction(this CancelType cancelType) =>
        cancelType == CancelType.Action || cancelType == CancelType.Recovery;
}
