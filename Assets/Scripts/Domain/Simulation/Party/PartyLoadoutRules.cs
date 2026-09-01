using System.Collections.Generic;

/// <summary>阵容结构校验失败的稳定原因。</summary>
public enum PartyLoadoutValidationError
{
    /// <summary>阵容结构有效。</summary>
    None = 0,

    /// <summary>槽位数量不在 1～3。</summary>
    InvalidSlotCount = 1,

    /// <summary>开局槽索引越界。</summary>
    InvalidStartingSlot = 2,

    /// <summary>开局槽没有角色。</summary>
    EmptyStartingSlot = 3,

    /// <summary>两个非空槽使用了相同 CharacterId。</summary>
    DuplicateCharacterId = 4,
}

/// <summary>不依赖 Unity 资产的阵容槽位与稳定 Id 校验规则。</summary>
public static class PartyLoadoutRules
{
    /// <summary>纯模拟与网络载荷共享的最大出战槽数。</summary>
    public const int MaxMembers = 3;

    /// <summary>校验 1～3 槽、非空开局角色与非空 Id 唯一性；空白 Id 表示合法空槽。</summary>
    public static bool TryValidate(
        IReadOnlyList<CharacterId> slots,
        int startingSlot,
        out PartyLoadoutValidationError error)
    {
        error = PartyLoadoutValidationError.None;
        if (slots == null || slots.Count < 1 || slots.Count > MaxMembers)
        {
            error = PartyLoadoutValidationError.InvalidSlotCount;
            return false;
        }

        if (startingSlot < 0 || startingSlot >= slots.Count)
        {
            error = PartyLoadoutValidationError.InvalidStartingSlot;
            return false;
        }

        if (!slots[startingSlot].IsValid)
        {
            error = PartyLoadoutValidationError.EmptyStartingSlot;
            return false;
        }

        var ids = new HashSet<CharacterId>();
        for (int i = 0; i < slots.Count; i++)
        {
            CharacterId id = slots[i];
            if (!id.IsValid)
                continue;
            if (!ids.Add(id))
            {
                error = PartyLoadoutValidationError.DuplicateCharacterId;
                return false;
            }
        }

        return true;
    }
}
