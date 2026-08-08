/// <summary>TargetAdhesion 纯计算入参（不依赖 Timeline Notify 类型）。</summary>
public readonly struct ActionMotionAdhesionParams
{
    /// <summary>窗口起始逻辑帧（含）。</summary>
    public int StartFrame { get; }

    /// <summary>窗口结束逻辑帧（含）。</summary>
    public int EndFrame { get; }

    /// <summary>沿玩家→敌人连线、相对敌人中心的水平偏移（毫米）。</summary>
    public int HorizontalOffsetMm { get; }

    /// <summary>沿连线法线的侧向偏移（毫米）。</summary>
    public int LateralOffsetMm { get; }

    /// <summary>单帧修正上限（毫米）。</summary>
    public int MaxCorrectionMmPerFrame { get; }

    /// <summary>最大捕获距离（毫米）；0=不限制。</summary>
    public int MaxAcquireDistanceMm { get; }

    /// <summary>连线与角色朝向夹角上限（毫度）；0=不限制。</summary>
    public int MaxAngleMilliDeg { get; }

    /// <summary>构造吸附参数；修正上限至少为 1。</summary>
    public ActionMotionAdhesionParams(
        int startFrame,
        int endFrame,
        int horizontalOffsetMm,
        int lateralOffsetMm,
        int maxCorrectionMmPerFrame,
        int maxAcquireDistanceMm,
        int maxAngleMilliDeg)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
        HorizontalOffsetMm = horizontalOffsetMm;
        LateralOffsetMm = lateralOffsetMm;
        MaxCorrectionMmPerFrame = maxCorrectionMmPerFrame < 1 ? 1 : maxCorrectionMmPerFrame;
        MaxAcquireDistanceMm = maxAcquireDistanceMm < 0 ? 0 : maxAcquireDistanceMm;
        MaxAngleMilliDeg = maxAngleMilliDeg < 0 ? 0 : maxAngleMilliDeg;
    }

    /// <summary>指定帧是否落在闭区间窗口内。</summary>
    public bool IsActiveAtFrame(int frame) =>
        EndFrame >= StartFrame && frame >= StartFrame && frame <= EndFrame;
}
