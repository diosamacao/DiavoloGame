/// <summary>Action 基础位移权威归类（Wave 0 审计用；不改变 Runtime 分支）。</summary>
public enum ActionMotionSourceKind
{
    /// <summary>无烘焙表且无脚本位移窗口。</summary>
    None = 0,

    /// <summary>仅烘焙运动表就绪。</summary>
    Baked = 1,

    /// <summary>仅 Timeline 脚本位移窗口。</summary>
    Scripted = 2,

    /// <summary>烘焙表与脚本位移并存（或其它互斥冲突）。</summary>
    Conflict = 3,
}
