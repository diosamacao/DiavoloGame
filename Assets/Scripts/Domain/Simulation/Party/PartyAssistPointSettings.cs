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
