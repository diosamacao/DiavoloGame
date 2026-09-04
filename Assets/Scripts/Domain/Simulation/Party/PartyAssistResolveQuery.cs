/// <summary>切人裁定输入：闪光与突击武装。上场 AssistStyle 由 Coordinator 按目标槽读取。</summary>
public readonly struct PartyAssistResolveQuery
{
    /// <summary>无闪光、未武装突击：普通 DualPresence SwitchIn。</summary>
    public static PartyAssistResolveQuery None { get; }

    /// <summary>组装一次切人查询；HasCue 为 false 时忽略 Kind / CueOwnerId。</summary>
    public PartyAssistResolveQuery(
        bool hasCue,
        AssistCueKind cueKind,
        bool requiresRanged,
        bool assistFollowUpArmed,
        SimActorId cueOwnerId = default)
    {
        HasCue = hasCue;
        CueKind = cueKind;
        RequiresRanged = requiresRanged;
        AssistFollowUpArmed = assistFollowUpArmed;
        CueOwnerId = hasCue ? cueOwnerId : default;
    }

    /// <summary>本帧是否存在有效 AssistCue。</summary>
    public bool HasCue { get; }

    /// <summary>资产闪光种类；0 点时 Coordinator 再降为 Red。</summary>
    public AssistCueKind CueKind { get; }

    /// <summary>远程点名金光。</summary>
    public bool RequiresRanged { get; }

    /// <summary>支援突击武装中首版禁止再切人。</summary>
    public bool AssistFollowUpArmed { get; }

    /// <summary>闪光所属进攻者；InstantReplace 上场 Guard Relocate 必须钉此目标。</summary>
    public SimActorId CueOwnerId { get; }
}
