
/// <summary>座位协调器输出的确定性换人结果。</summary>
public readonly struct PartySwitchCommand
{
    /// <summary>创建已裁定的换人命令。</summary>
    public PartySwitchCommand(
        int fromSlot,
        int toSlot,
        PartySwitchKind kind,
        PartySwitchPresentation presentation,
        int spendAssistPoints = 0,
        SimActorId cueOwnerId = default)
    {
        FromSlot = fromSlot;
        ToSlot = toSlot;
        Kind = kind;
        Presentation = presentation;
        SpendAssistPoints = spendAssistPoints < 0 ? 0 : spendAssistPoints;
        CueOwnerId = cueOwnerId;
    }

    /// <summary>退场角色槽位。</summary>
    public int FromSlot { get; }

    /// <summary>上场角色槽位。</summary>
    public int ToSlot { get; }

    /// <summary>上场动作类别。</summary>
    public PartySwitchKind Kind { get; }

    /// <summary>新旧角色的并存策略。</summary>
    public PartySwitchPresentation Presentation { get; }

    /// <summary>本命令已扣除的支援点；红光/普通切为 0。</summary>
    public int SpendAssistPoints { get; }

    /// <summary>闪光敌人；InstantReplace 上场钉 SelectedTarget。普通切为 Invalid。</summary>
    public SimActorId CueOwnerId { get; }
}
