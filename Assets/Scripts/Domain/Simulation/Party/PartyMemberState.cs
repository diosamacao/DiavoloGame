using System;

/// <summary>阵容槽位在战斗中的激活与退场生命周期。</summary>
public enum PartyMemberState
{
    /// <summary>Loadout 中未装配角色的槽位，永远不可被切出。</summary>
    Empty = 0,

    /// <summary>后台待命，可被下一次顺序切人选中。</summary>
    Inactive = 1,

    /// <summary>当前接收玩法输入并参与战斗的角色。</summary>
    Active = 2,

    /// <summary>普通切人后仍在收招，不可立即再次切出。</summary>
    Exiting = 3,

    /// <summary>已死亡，顺序切人必须跳过。</summary>
    Dead = 4,
}
