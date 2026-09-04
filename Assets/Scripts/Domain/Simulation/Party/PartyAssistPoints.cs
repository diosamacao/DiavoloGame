/// <summary>队共享支援点口袋；不进角色 Numeric。扣费只在 Coordinator 裁定成功时发生。</summary>
public sealed class PartyAssistPoints
{
    /// <summary>支援点上限。</summary>
    public const int Max = 6;

    /// <summary>开局点数。</summary>
    public const int Starting = 3;

    /// <summary>极限支援消耗。</summary>
    public const int AssistCost = 1;

    /// <summary>终结技起手回复。</summary>
    public const int UltimateGrant = 3;

    int _current = Starting;

    /// <summary>当前点数，已夹在 0～Max。</summary>
    public int Current => _current;

    /// <summary>是否够付一次极限支援。</summary>
    public bool CanSpendAssist => _current >= AssistCost;

    /// <summary>裁定成功时扣 1 点；不足返回 false 且不改值。</summary>
    public bool TrySpendAssist()
    {
        if (_current < AssistCost)
            return false;
        _current -= AssistCost;
        return true;
    }

    /// <summary>回复点数并夹到上限。</summary>
    public void Grant(int amount)
    {
        if (amount <= 0)
            return;
        int next = _current + amount;
        _current = next > Max ? Max : next;
    }
}
