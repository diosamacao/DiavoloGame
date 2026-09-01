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

/// <summary>在角色快照 FlagsPacked 低位编码阵容槽状态，避免另开平行复制状态。</summary>
public static class PartyReplicationPacking
{
    const int StateMask = 0x7;

    /// <summary>保留其它标志位并写入三位 PartyMemberState。</summary>
    public static int WithMemberState(int flagsPacked, PartyMemberState state)
    {
        int value = (int)state;
        if (value < 0 || value > StateMask)
            throw new ArgumentOutOfRangeException(nameof(state));
        return (flagsPacked & ~StateMask) | value;
    }

    /// <summary>从低三位读取状态；非法线值明确失败，禁止把未知协议值当 Active。</summary>
    public static PartyMemberState ReadMemberState(int flagsPacked)
    {
        var state = (PartyMemberState)(flagsPacked & StateMask);
        if (!Enum.IsDefined(typeof(PartyMemberState), state))
            throw new InvalidOperationException($"未知 PartyMemberState 线值 {(int)state}。");
        return state;
    }
}
