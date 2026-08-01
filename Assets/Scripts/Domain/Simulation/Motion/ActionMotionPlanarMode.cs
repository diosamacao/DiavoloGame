/// <summary>烘焙/查表时水平位移投影策略。</summary>
public enum ActionMotionPlanarMode
{
    /// <summary>保留本地 XZ。</summary>
    FullPlanar = 0,

    /// <summary>投影到本地 +Z（前向）。</summary>
    ForwardOnly = 1,
}
