using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>查询场上全部玩家权威根，供敌人感知选最近目标。</summary>
public sealed class GetPlayerRootsQuery : ArchitectureQueryBase<IReadOnlyList<Transform>>
{
    static readonly Transform[] Empty = Array.Empty<Transform>();

    /// <summary>只读花名册缓存列表；无服务时返回空数组。</summary>
    protected override IReadOnlyList<Transform> OnQuery()
    {
        IReadOnlyList<Transform> roots = this.GetSystem<LocalPlayerService>()?.PlayerRoots;
        return roots ?? Empty;
    }
}
