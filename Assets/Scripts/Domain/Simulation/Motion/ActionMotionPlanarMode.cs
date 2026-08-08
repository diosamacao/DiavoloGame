/// <summary>烘焙/查表时水平位移投影策略。</summary>
public enum ActionMotionPlanarMode
{
    /// <summary>保留本地 XZ（侧闪/横移斩等玩法横移用）。</summary>
    FullPlanar = 0,

    /// <summary>
    /// 正确前向提取：丢弃本地 X，保留累计轨迹的 Z 差分（= 原始 dz）。
    /// 纯左右摆不再产生前进，也不再推逻辑根横跳。
    /// </summary>
    ForwardSigned = 2,

    // 1 曾为 ForwardOnly（旧保模长语义），Wave 2.5 已删除；资产勿再写入。
}
