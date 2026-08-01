/// <summary>动作图取消路由类型；同帧重叠时 Perfect 优先于 Normal。</summary>
public enum CancelWindowType
{
    /// <summary>普通取消窗口，也是顺序组自动链使用的类型。</summary>
    Normal = 0,

    /// <summary>精确取消窗口；与 Normal 重叠且意图相同时优先。</summary>
    Perfect = 1,
}
