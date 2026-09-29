using System;

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
