/// <summary>角色唯一目标状态的只读逻辑快照。</summary>
public readonly struct CharacterTargetingSnapshot
{
    /// <summary>创建仅含稳定目标身份的快照。</summary>
    public CharacterTargetingSnapshot(SimActorId selectedTargetId)
    {
        SelectedTargetId = selectedTargetId;
    }

    /// <summary>当前唯一 SelectedTarget；无目标时 Invalid。</summary>
    public SimActorId SelectedTargetId { get; }

    /// <summary>当前是否存在有效目标身份。</summary>
    public bool HasSelectedTarget => SelectedTargetId.IsValid;
}
