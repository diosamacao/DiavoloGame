using System;

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
