/// <summary>烘焙/查表时水平位移投影策略。</summary>
public enum ActionMotionPlanarMode
{
    /// <summary>保留本地 XZ（侧闪/横移斩等玩法横移用）。</summary>
    FullPlanar = 0,

    /// <summary>
    /// 将累计轨迹投影到整个动作的起终点连线，保留沿线推进与回撤，末帧视觉残差为零。
    /// 起终点重合时逻辑位移为零；数值 2 保持既有资产的模式选择。
    /// </summary>
    EndpointSigned = 2,

    // 1 曾为 ForwardOnly（旧保模长语义），Wave 2.5 已删除；资产勿再写入。
}
