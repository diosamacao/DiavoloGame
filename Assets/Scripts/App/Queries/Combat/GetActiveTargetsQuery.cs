using System.Collections.Generic;

/// <summary>查询当前全部可命中目标；该查询不修改 TargetSystem 状态。</summary>
public sealed class GetActiveTargetsQuery : ArchitectureQueryBase<IReadOnlyList<IHurtboxTarget>>
{
    /// <summary>返回目标系统当前维护的只读目标列表。</summary>
    protected override IReadOnlyList<IHurtboxTarget> OnQuery()
    {
        return this.GetSystem<TargetSystem>()?.ActiveTargets;
    }
}
