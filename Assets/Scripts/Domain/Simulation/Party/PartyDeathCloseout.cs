using System;

/// <summary>协调器原子提交死亡后产生的换槽或队灭结果。</summary>
public readonly struct PartyDeathCloseout
{
    /// <summary>创建一次死亡收尾结果；toSlot 小于 0 表示全队已灭。</summary>
    public PartyDeathCloseout(int fromSlot, int toSlot)
    {
        FromSlot = fromSlot;
        ToSlot = toSlot;
    }

    /// <summary>死亡槽位。</summary>
    public int FromSlot { get; }

    /// <summary>自动上场槽位；队灭时为 -1。</summary>
    public int ToSlot { get; }

    /// <summary>本次收尾是否没有可用成员。</summary>
    public bool PartyWiped => ToSlot < 0;
}
