using UnityEngine;

/// <summary>
/// 内层走跑可恢复态。字段来自现有 Context / RootMotion / FootCycle，不是第二套相位袋。
/// 本机 Tick 后由 <see cref="LocomotionStateMachine.Capture"/> 捕获；V2 协议需显式携带 PhaseFrame。
/// </summary>
public readonly struct LocomotionSavedState
{
    /// <summary>把 V2 完整角色快照转换为可恢复的整数走跑状态。</summary>
    public static LocomotionSavedState FromSnapshot(in ActorReplicationSnapshot snapshot)
    {
        var key = (AnimationKey)snapshot.LocomotionPhase;
        LocomotionPhase phase = PhaseFromAnimationKey(key);
        LocomotionGait gait = DecodeGait(snapshot.Gait, key);
        MoveCardinal cardinal = DecodeCardinal(snapshot.Cardinal);
        Vector3 facing = FacingFromMilliDeg(snapshot.FacingMilliDeg);
        bool rootMotionActive = phase == LocomotionPhase.Stop
            || phase == LocomotionPhase.PivotTurn;
        return new LocomotionSavedState(
            phase,
            gait,
            key,
            snapshot.LocomotionPhaseFrame,
            0,
            0,
            cardinal,
            0,
            key,
            gait,
            cardinal,
            key,
            key == AnimationKey.StartEnd,
            facing,
            facing,
            facing,
            false,
            rootMotionActive,
            key,
            MotionQuantization.MilliDegToDegrees(snapshot.FacingMilliDeg),
            FootSide.Left,
            false,
            phase != LocomotionPhase.Gait && phase != LocomotionPhase.Start,
            rootMotionBasisIsCurrentFacing: true);
    }

    /// <summary>AnimationKey（快照 LocomotionPhase 字节）映射到内层相位。</summary>
    public static LocomotionPhase PhaseFromAnimationKey(AnimationKey key)
    {
        switch (key)
        {
            case AnimationKey.Start:
            case AnimationKey.WalkStart:
            case AnimationKey.WalkStartLeft:
            case AnimationKey.WalkStartRight:
                return LocomotionPhase.Start;
            case AnimationKey.PivotTurn:
                return LocomotionPhase.PivotTurn;
            case AnimationKey.StopL:
            case AnimationKey.StopR:
            case AnimationKey.StartEnd:
                return LocomotionPhase.Stop;
            case AnimationKey.Walk:
            case AnimationKey.WalkLeft:
            case AnimationKey.WalkRight:
            case AnimationKey.Run:
            case AnimationKey.Sprint:
                return LocomotionPhase.Gait;
            default:
                return LocomotionPhase.Idle;
        }
    }

    /// <summary>完整捕获构造；由 <see cref="LocomotionStateMachine.Capture"/> 调用。</summary>
    public LocomotionSavedState(
        LocomotionPhase phase,
        LocomotionGait gait,
        AnimationKey animationKey,
        int phaseFrame,
        int runHoldFrames,
        int gaitInputGapFrames,
        MoveCardinal gaitCardinal,
        int gaitCardinalDwellFrames,
        AnimationKey activeStartKey,
        LocomotionGait activeStartGait,
        MoveCardinal activeStartCardinal,
        AnimationKey stopKey,
        bool stopFromStart,
        Vector3 stopEnterFacing,
        Vector3 pivotTarget,
        Vector3 pivotEnterFacing,
        bool pivotMoveLatched,
        bool rootMotionActive,
        AnimationKey rootMotionKey,
        float rootMotionBasisYaw,
        FootSide lastPlanted,
        bool hasPlantRecord,
        bool footFrozen,
        bool rootMotionBasisIsCurrentFacing = false)
    {
        Phase = phase;
        Gait = gait;
        AnimationKey = animationKey;
        PhaseFrame = phaseFrame;
        RunHoldFrames = runHoldFrames;
        GaitInputGapFrames = gaitInputGapFrames;
        GaitCardinal = gaitCardinal;
        GaitCardinalDwellFrames = gaitCardinalDwellFrames;
        ActiveStartKey = activeStartKey;
        ActiveStartGait = activeStartGait;
        ActiveStartCardinal = activeStartCardinal;
        StopKey = stopKey;
        StopFromStart = stopFromStart;
        StopEnterFacing = stopEnterFacing;
        PivotTarget = pivotTarget;
        PivotEnterFacing = pivotEnterFacing;
        PivotMoveLatched = pivotMoveLatched;
        RootMotionActive = rootMotionActive;
        RootMotionKey = rootMotionKey;
        RootMotionBasisYaw = rootMotionBasisYaw;
        LastPlanted = lastPlanted;
        HasPlantRecord = hasPlantRecord;
        FootFrozen = footFrozen;
        RootMotionBasisIsCurrentFacing = rootMotionBasisIsCurrentFacing;
    }

    /// <summary>内层相位。</summary>
    public LocomotionPhase Phase { get; }
    /// <summary>稳态步态。</summary>
    public LocomotionGait Gait { get; }
    /// <summary>当前 Clip 键。</summary>
    public AnimationKey AnimationKey { get; }
    /// <summary>当前动画键内的整数逻辑帧。</summary>
    public int PhaseFrame { get; }
    /// <summary>Run 满输入累计逻辑帧。</summary>
    public int RunHoldFrames { get; }
    /// <summary>Gait 无输入宽限累计逻辑帧。</summary>
    public int GaitInputGapFrames { get; }
    /// <summary>Gait 循环滞回 cardinal。</summary>
    public MoveCardinal GaitCardinal { get; }
    /// <summary>当前 cardinal 驻留帧。</summary>
    public int GaitCardinalDwellFrames { get; }
    /// <summary>Start 闩定 Key。</summary>
    public AnimationKey ActiveStartKey { get; }
    /// <summary>Start 闩定步态档。</summary>
    public LocomotionGait ActiveStartGait { get; }
    /// <summary>Start 闩定 cardinal。</summary>
    public MoveCardinal ActiveStartCardinal { get; }
    /// <summary>急停 Key。</summary>
    public AnimationKey StopKey { get; }
    /// <summary>急停是否来自 Start。</summary>
    public bool StopFromStart { get; }
    /// <summary>急停进入朝向。</summary>
    public Vector3 StopEnterFacing { get; }
    /// <summary>Pivot 目标朝向。</summary>
    public Vector3 PivotTarget { get; }
    /// <summary>Pivot 进入朝向。</summary>
    public Vector3 PivotEnterFacing { get; }
    /// <summary>本次 Pivot 是否出现过移动。</summary>
    public bool PivotMoveLatched { get; }
    /// <summary>烘焙根位移是否进行中。</summary>
    public bool RootMotionActive { get; }
    /// <summary>烘焙轨 Key。</summary>
    public AnimationKey RootMotionKey { get; }
    /// <summary>烘焙局部→世界基偏航。</summary>
    public float RootMotionBasisYaw { get; }
    /// <summary>最近落脚。</summary>
    public FootSide LastPlanted { get; }
    /// <summary>是否已有真实落脚记录。</summary>
    public bool HasPlantRecord { get; }
    /// <summary>落脚采样是否冻结。</summary>
    public bool FootFrozen { get; }
    /// <summary>true 表示复制仅携带当前朝向，Restore 需反推 Pivot 进入基；本地 Capture 为 false。</summary>
    public bool RootMotionBasisIsCurrentFacing { get; }

    /// <summary>Clip 为 Sprint 时以片为准，避免出招期间快照 Gait 被写成 Walk。</summary>
    public static LocomotionGait DecodeGait(byte raw, AnimationKey key)
    {
        if (key == AnimationKey.Sprint)
            return LocomotionGait.Sprint;
        if (key == AnimationKey.Run)
            return LocomotionGait.Run;
        if (raw == (byte)LocomotionGait.Sprint)
            return LocomotionGait.Sprint;
        if (raw == (byte)LocomotionGait.Run)
            return LocomotionGait.Run;
        return LocomotionGait.Walk;
    }

    /// <summary>把复制字节严格映射为 Cardinal；未知值返回 None。</summary>
    public static MoveCardinal DecodeCardinal(byte raw)
    {
        if (raw == (byte)MoveCardinal.Back)
            return MoveCardinal.Back;
        if (raw == (byte)MoveCardinal.Left)
            return MoveCardinal.Left;
        if (raw == (byte)MoveCardinal.Right)
            return MoveCardinal.Right;
        if (raw == (byte)MoveCardinal.Forward)
            return MoveCardinal.Forward;
        return MoveCardinal.None;
    }

    /// <summary>把整数毫度偏航还原为水平单位方向。</summary>
    public static Vector3 FacingFromMilliDeg(int facingMilliDeg)
    {
        float yaw = MotionQuantization.MilliDegToDegrees(facingMilliDeg);
        return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    }
}
