using System;

/// <summary>队共享支援点口袋的可配数值；默认对齐首版 6/3/1/+3。无 Unity 属性（Simulation 程序集 noEngineReferences）。</summary>
[Serializable]
public struct PartyAssistPointSettings
{
    /// <summary>支援点上限。</summary>
    public int max;

    /// <summary>开局点数，创建口袋时写入。</summary>
    public int starting;

    /// <summary>金光极限支援一次消耗。</summary>
    public int assistCost;

    /// <summary>终结技起手回复。</summary>
    public int ultimateGrant;

    /// <summary>首版默认：上限 6、开局 3、耗 1、Ult +3。</summary>
    public static PartyAssistPointSettings Default => new()
    {
        max = PartyAssistPoints.Max,
        starting = PartyAssistPoints.Starting,
        assistCost = PartyAssistPoints.AssistCost,
        ultimateGrant = PartyAssistPoints.UltimateGrant,
    };

    /// <summary>全 0 视为未配置，用首版默认；避免 Loadout 旧资产缺字段时开局 0 点。</summary>
    public bool IsUnset => max == 0 && starting == 0 && assistCost == 0 && ultimateGrant == 0;

    /// <summary>把非法值夹回默认下限，供 Loadout / 口袋构造使用。</summary>
    public PartyAssistPointSettings Sanitized()
    {
        if (IsUnset)
            return Default;

        int cap = max > 0 ? max : PartyAssistPoints.Max;
        int start = starting < 0 ? 0 : starting;
        if (start > cap)
            start = cap;
        return new PartyAssistPointSettings
        {
            max = cap,
            starting = start,
            assistCost = assistCost > 0 ? assistCost : PartyAssistPoints.AssistCost,
            ultimateGrant = ultimateGrant < 0 ? 0 : ultimateGrant,
        };
    }
}

/// <summary>队共享支援点口袋；不进角色 Numeric。扣费只在 Coordinator 裁定成功时发生。</summary>
public sealed class PartyAssistPoints
{
    /// <summary>支援点上限默认值。</summary>
    public const int Max = 6;

    /// <summary>开局点数默认值。</summary>
    public const int Starting = 3;

    /// <summary>极限支援消耗默认值。</summary>
    public const int AssistCost = 1;

    /// <summary>终结技起手回复默认值。</summary>
    public const int UltimateGrant = 3;

    readonly int _max;
    readonly int _assistCost;
    readonly int _ultimateGrant;
    int _current;

    /// <summary>用首版默认上限/开局/消耗创建口袋。</summary>
    public PartyAssistPoints()
        : this(PartyAssistPointSettings.Default)
    {
    }

    /// <summary>按 Loadout 或测试给定数值创建口袋；非法值夹到默认下限。</summary>
    public PartyAssistPoints(in PartyAssistPointSettings settings)
    {
        PartyAssistPointSettings sane = settings.Sanitized();
        _max = sane.max;
        _assistCost = sane.assistCost;
        _ultimateGrant = sane.ultimateGrant;
        _current = sane.starting;
    }

    /// <summary>当前点数，已夹在 0～Capacity。</summary>
    public int Current => _current;

    /// <summary>本口袋上限。</summary>
    public int Capacity => _max;

    /// <summary>金光极限支援一次消耗。</summary>
    public int Cost => _assistCost;

    /// <summary>终结技起手回复量。</summary>
    public int UltimateGrantAmount => _ultimateGrant;

    /// <summary>是否够付一次极限支援。</summary>
    public bool CanSpendAssist => _current >= _assistCost;

    /// <summary>裁定成功时扣 Cost；不足返回 false 且不改值。</summary>
    public bool TrySpendAssist()
    {
        if (_current < _assistCost)
            return false;
        _current -= _assistCost;
        return true;
    }

    /// <summary>回复点数并夹到上限。</summary>
    public void Grant(int amount)
    {
        if (amount <= 0)
            return;
        int next = _current + amount;
        _current = next > _max ? _max : next;
    }

    /// <summary>终结技起手回复 UltimateGrantAmount。</summary>
    public void GrantUltimate() => Grant(_ultimateGrant);
}
