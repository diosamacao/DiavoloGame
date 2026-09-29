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
