using System;

/// <summary>
/// Wave 3 / N5：同键 Special 在多候选中按能量选 EX 或普通形态；纯选择，不扣费。
/// </summary>
public static class ActionEnergyFormSelector
{
    /// <summary>
    /// 优先返回可负担的 ExSpecial 下标；否则可负担的非 EX；仅单候选时即使不够费也返回 0（交 TryStart 拒绝）。
    /// </summary>
    public static bool TryFindIndex(
        int candidateCount,
        Func<int, ActionResourceTag> getTag,
        Func<int, bool> canAfford,
        out int index)
    {
        index = -1;
        if (candidateCount <= 0 || getTag == null)
            return false;

        Func<int, bool> afford = canAfford ?? (_ => true);
        int exAffordable = -1;
        int nonExAffordable = -1;

        for (int i = 0; i < candidateCount; i++)
        {
            bool isEx = getTag(i) == ActionResourceTag.ExSpecial;
            bool ok = afford(i);
            if (isEx)
            {
                if (ok && exAffordable < 0)
                    exAffordable = i;
            }
            else if (ok && nonExAffordable < 0)
            {
                nonExAffordable = i;
            }
        }

        if (exAffordable >= 0)
        {
            index = exAffordable;
            return true;
        }

        if (nonExAffordable >= 0)
        {
            index = nonExAffordable;
            return true;
        }

        // 单入口图：仍交出选招，由 ActionSim.TryStart/CanAfford 拒绝并保留缓冲
        if (candidateCount == 1)
        {
            index = 0;
            return true;
        }

        return false;
    }

    /// <summary>HUD：下一发 Special 是否会走 EX 形态。</summary>
    public static bool WouldSelectExSpecial(
        int candidateCount,
        Func<int, ActionResourceTag> getTag,
        Func<int, bool> canAfford)
    {
        return TryFindIndex(candidateCount, getTag, canAfford, out int index)
            && index >= 0
            && getTag(index) == ActionResourceTag.ExSpecial;
    }
}
