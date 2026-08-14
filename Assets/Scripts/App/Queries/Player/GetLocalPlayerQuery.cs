/// <summary>查询当前拥有输入与相机的本机玩家；未登记时为空。</summary>
public sealed class GetLocalPlayerQuery : ArchitectureQueryBase<ILocalPlayer>
{
    /// <summary>只读 LocalPlayerService.Local。</summary>
    protected override ILocalPlayer OnQuery()
    {
        return this.GetSystem<LocalPlayerService>()?.Local;
    }
}
