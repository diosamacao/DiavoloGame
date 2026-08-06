/// <summary>动作结束或打断时，视觉残差如何回到逻辑锚点。</summary>
public enum VisualResidualExitPolicy
{
    /// <summary>正常结束：期望末帧残差已接近 0（烘焙校验）；否则仍 Snap。</summary>
    RequireZeroAtEnd = 0,

    /// <summary>取消/受击：短时表现插值回原点，不推动逻辑根。</summary>
    BlendToZero = 1,

    /// <summary>传送/死亡等：立即清零。</summary>
    SnapToZero = 2,
}
