/// <summary>独立 CancelWindow 的图路由语义；重叠时同一节点 Intent 优先使用 Perfect。</summary>
public enum CancelWindowType
{
    /// <summary>普通取消窗口，也是顺序组自动链使用的类型。</summary>
    Normal = 0,

    /// <summary>精确取消窗口；与 Normal 重叠且节点 Intent 相同时优先。</summary>
    Perfect = 1,
}
