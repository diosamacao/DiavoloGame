using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家单座位的出战阵容；按数组顺序单向循环，最多三名角色。</summary>
[CreateAssetMenu(fileName = "PartyLoadout", menuName = "ACT/Party/Party Loadout")]
public sealed class PartyLoadout : ScriptableObject
{
    public const int MaxMembers = 3;

    [SerializeField] CharacterDefinition[] members = Array.Empty<CharacterDefinition>();
    [SerializeField] int startingSlot = 0;

    /// <summary>按切人顺序排列的角色定义。</summary>
    public IReadOnlyList<CharacterDefinition> Members =>
        members ?? Array.Empty<CharacterDefinition>();

    /// <summary>开局激活槽位。</summary>
    public int StartingSlot => startingSlot;

    /// <summary>当前声明的槽位数量。</summary>
    public int Count => members?.Length ?? 0;

    /// <summary>返回开局角色；Loadout 非法时返回 null。</summary>
    public CharacterDefinition StartingMember =>
        members != null
        && startingSlot >= 0
        && startingSlot < members.Length
            ? members[startingSlot]
            : null;

    /// <summary>校验 1～3 槽、有效开局角色与非空角色的 CharacterId 唯一性；中间空槽合法。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        UnityEngine.Object logContext = context != null ? context : this;
        int count = members?.Length ?? 0;
        var ids = new CharacterId[count];
        for (int i = 0; i < count; i++)
            ids[i] = members[i] != null ? members[i].Id : default;

        if (!PartyLoadoutRules.TryValidate(ids, startingSlot, out PartyLoadoutValidationError error))
        {
            Debug.LogError(BuildValidationError(error), logContext);
            return false;
        }

        bool valid = true;
        for (int i = 0; i < members.Length; i++)
        {
            CharacterDefinition member = members[i];
            if (member == null)
                continue;

            if (!member.Validate(logContext))
                valid = false;
        }

        return valid;
    }

    /// <summary>把纯规则错误转换为面向 Inspector 的明确日志。</summary>
    static string BuildValidationError(PartyLoadoutValidationError error)
    {
        switch (error)
        {
            case PartyLoadoutValidationError.InvalidSlotCount:
                return $"PartyLoadout: 出战槽位数量必须为 1～{MaxMembers}。";
            case PartyLoadoutValidationError.InvalidStartingSlot:
                return "PartyLoadout: StartingSlot 超出阵容范围。";
            case PartyLoadoutValidationError.EmptyStartingSlot:
                return "PartyLoadout: StartingSlot 必须指向非空角色。";
            case PartyLoadoutValidationError.DuplicateCharacterId:
                return "PartyLoadout: CharacterId 不得重复。";
            default:
                return "PartyLoadout: 阵容配置无效。";
        }
    }
}
