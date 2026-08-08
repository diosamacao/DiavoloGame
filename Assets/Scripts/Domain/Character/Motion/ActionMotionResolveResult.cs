/// <summary>MotionCommand 解析结果。</summary>
public readonly struct ActionMotionResolveResult
{
    /// <summary>未应用。</summary>
    public static ActionMotionResolveResult NotApplied => default;

    /// <summary>是否已写入 Motor。</summary>
    public bool Applied { get; }

    /// <summary>解析后水平毫米位置。</summary>
    public SimVec2 ResolvedMm { get; }

    /// <summary>解析后朝向（度）。</summary>
    public float FacingDegrees { get; }

    /// <summary>建议软体抑制帧。</summary>
    public int SoftBodySuppressFrames { get; }

    /// <summary>构造已应用结果。</summary>
    public ActionMotionResolveResult(bool applied, SimVec2 resolvedMm, float facingDegrees, int softBodySuppressFrames)
    {
        Applied = applied;
        ResolvedMm = resolvedMm;
        FacingDegrees = facingDegrees;
        SoftBodySuppressFrames = softBodySuppressFrames;
    }
}
