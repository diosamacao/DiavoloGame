/// <summary>换人命令对应的上场招式类别。</summary>
public enum PartySwitchKind
{
    /// <summary>无支援窗口时播放普通登场动作。</summary>
    SwitchIn = 0,
}

/// <summary>换人时新旧角色是否允许短暂同时留场。</summary>
public enum PartySwitchPresentation
{
    /// <summary>旧角色收招期间，新角色同时播放 SwitchIn。</summary>
    DualPresence = 0,

    /// <summary>旧角色当帧退场，新角色直接播放支援动作。</summary>
    InstantReplace = 1,
}

/// <summary>座位协调器输出的确定性换人结果。</summary>
public readonly struct PartySwitchCommand
{
    /// <summary>创建已裁定的换人命令。</summary>
    public PartySwitchCommand(
        int fromSlot,
        int toSlot,
        PartySwitchKind kind,
        PartySwitchPresentation presentation)
    {
        FromSlot = fromSlot;
        ToSlot = toSlot;
        Kind = kind;
        Presentation = presentation;
    }

    /// <summary>退场角色槽位。</summary>
    public int FromSlot { get; }

    /// <summary>上场角色槽位。</summary>
    public int ToSlot { get; }

    /// <summary>上场动作类别。</summary>
    public PartySwitchKind Kind { get; }

    /// <summary>新旧角色的并存策略。</summary>
    public PartySwitchPresentation Presentation { get; }
}

/// <summary>计算普通换人的确定性逻辑落点，不依赖相机或 Unity Transform。</summary>
public static class PartySwitchPlacement
{
    /// <summary>普通换人时新角色位于旧角色局部右侧 0.6 米。</summary>
    public const int NormalSwitchRightOffsetMm = 1000;

    /// <summary>按退场角色的逻辑朝向计算新角色右侧落点。</summary>
    public static SimVec2 ResolveNormalSwitchPosition(
        SimVec2 outgoingPosition,
        int outgoingFacingMilliDeg)
    {
        CharacterMotorSim.RotateLocalToWorld(
            outgoingFacingMilliDeg,
            NormalSwitchRightOffsetMm,
            0,
            out int offsetXMm,
            out int offsetZMm);
        return new SimVec2(
            outgoingPosition.X + offsetXMm,
            outgoingPosition.Z + offsetZMm);
    }
}
