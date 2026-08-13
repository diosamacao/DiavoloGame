/// <summary>Camera/UI 只读访问 SelectedTarget 的表现映射契约。</summary>
public interface ILocalCameraTargetSource
{
    /// <summary>当前唯一目标身份快照。</summary>
    CharacterTargetingSnapshot TargetingSnapshot { get; }

    /// <summary>把当前目标身份映射到表现目标；不得触发重新选敌。</summary>
    bool TryGetSelectedTarget(out ITargetable target);
}
