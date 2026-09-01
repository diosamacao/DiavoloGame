using System;
using System.Collections.Generic;

/// <summary>按阵容槽位正序循环选择下一名可激活角色。</summary>
public static class PartySlotSelector
{
    /// <summary>
    /// 从当前槽后一位开始绕回查找 Inactive 槽；跳过 Empty、Active、Exiting 与 Dead。
    /// 找不到其他可激活角色时返回 false。
    /// </summary>
    public static bool TryFindNext(
        int activeIndex,
        IReadOnlyList<PartyMemberState> states,
        out int nextIndex)
    {
        nextIndex = -1;
        if (states == null || states.Count < 2)
            return false;
        if (activeIndex < 0 || activeIndex >= states.Count)
            throw new ArgumentOutOfRangeException(nameof(activeIndex));

        for (int offset = 1; offset < states.Count; offset++)
        {
            int candidate = (activeIndex + offset) % states.Count;
            if (states[candidate] != PartyMemberState.Inactive)
                continue;

            nextIndex = candidate;
            return true;
        }

        return false;
    }
}
