/// <summary>权威帧上一条有效的极限支援闪光；供 Coordinator 裁定切人 kind。</summary>
public readonly struct AssistCue
{
    /// <summary>组装一条已收集的闪光窗。</summary>
    public AssistCue(
        SimActorId ownerId,
        AssistCueKind kind,
        bool requiresRanged,
        int remainingFrames,
        int parryLocalXMm,
        int parryLocalYMm,
        int parryLocalZMm,
        int evadeLocalXMm,
        int evadeLocalYMm,
        int evadeLocalZMm)
    {
        OwnerId = ownerId;
        Kind = kind;
        RequiresRanged = requiresRanged;
        RemainingFrames = remainingFrames < 0 ? 0 : remainingFrames;
        ParryLocalXMm = parryLocalXMm;
        ParryLocalYMm = parryLocalYMm;
        ParryLocalZMm = parryLocalZMm;
        EvadeLocalXMm = evadeLocalXMm;
        EvadeLocalYMm = evadeLocalYMm;
        EvadeLocalZMm = evadeLocalZMm;
    }

    /// <summary>闪光所属进攻者。</summary>
    public SimActorId OwnerId { get; }

    /// <summary>资产上的 Gold/Red；0 点时由 Coordinator 对外视为 Red。</summary>
    public AssistCueKind Kind { get; }

    /// <summary>为 true 时上场必须是远程回避，否则按 Red 处理。</summary>
    public bool RequiresRanged { get; }

    /// <summary>窗剩余逻辑帧（含当前帧）。</summary>
    public int RemainingFrames { get; }

    /// <summary>招架点相对进攻者逻辑根的局部毫米偏移。</summary>
    public int ParryLocalXMm { get; }

    /// <summary>招架点局部 Y（毫米）。</summary>
    public int ParryLocalYMm { get; }

    /// <summary>招架点局部 Z，默认正前。</summary>
    public int ParryLocalZMm { get; }

    /// <summary>回避点局部 X。</summary>
    public int EvadeLocalXMm { get; }

    /// <summary>回避点局部 Y。</summary>
    public int EvadeLocalYMm { get; }

    /// <summary>回避点局部 Z。</summary>
    public int EvadeLocalZMm { get; }

    /// <summary>是否指向有效进攻者。</summary>
    public bool IsValid => OwnerId.IsValid && RemainingFrames > 0;
}
