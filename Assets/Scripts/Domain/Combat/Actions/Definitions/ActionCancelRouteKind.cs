/// <summary>单一 CancelWindow 被 Perfect 分割帧划分出的两个图路由通道。</summary>
public enum ActionCancelRouteKind
{
    /// <summary>窗口起点到 Perfect 分割帧之前。</summary>
    Cancel = 0,

    /// <summary>Perfect 分割帧及之后；未配置分割帧时不存在。</summary>
    PerfectCancel = 1,
}
