using System;
using System.Collections.Generic;

/// <summary>维护单座位阵容槽状态，并把单键输入裁定为顺序普通切人命令。</summary>
public sealed class PartyCombatCoordinator
{
    const int MaxMembers = 3;
    readonly PartyMemberState[] states;

    /// <summary>按 Loadout 槽占用表创建阵容状态；槽数必须为 1～3，开局槽必须有人。</summary>
    public PartyCombatCoordinator(IReadOnlyList<bool> occupiedSlots, int startingSlot)
    {
        if (occupiedSlots == null)
            throw new ArgumentNullException(nameof(occupiedSlots));
        if (occupiedSlots.Count < 1 || occupiedSlots.Count > MaxMembers)
            throw new ArgumentOutOfRangeException(nameof(occupiedSlots));
        if (startingSlot < 0 || startingSlot >= occupiedSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(startingSlot));
        if (!occupiedSlots[startingSlot])
            throw new ArgumentException("开局槽必须装配角色。", nameof(startingSlot));

        states = new PartyMemberState[occupiedSlots.Count];
        for (int i = 0; i < occupiedSlots.Count; i++)
            states[i] = occupiedSlots[i] ? PartyMemberState.Inactive : PartyMemberState.Empty;
        ActiveIndex = startingSlot;
        states[startingSlot] = PartyMemberState.Active;
    }

    /// <summary>当前接收玩法输入的槽位。</summary>
    public int ActiveIndex { get; private set; }

    /// <summary>供 HUD、测试与后续 Actor 宿主读取的槽状态。</summary>
    public IReadOnlyList<PartyMemberState> States => states;

    /// <summary>
    /// 解析一次无 Cue 的普通切人：旧槽进入 Exiting，新槽立即 Active，并输出 DualPresence。
    /// 没有下一名可激活角色时不改变状态。
    /// </summary>
    public bool TryResolveSwitchIn(out PartySwitchCommand command)
    {
        command = default;
        if (!PartySlotSelector.TryFindNext(ActiveIndex, states, out int nextIndex))
            return false;

        int previousIndex = ActiveIndex;
        states[previousIndex] = PartyMemberState.Exiting;
        states[nextIndex] = PartyMemberState.Active;
        ActiveIndex = nextIndex;
        command = new PartySwitchCommand(
            previousIndex,
            nextIndex,
            PartySwitchKind.SwitchIn,
            PartySwitchPresentation.DualPresence);
        return true;
    }

    /// <summary>普通退场动作完成后把 Exiting 槽转为后台 Inactive。</summary>
    public void CompleteExit(int slot)
    {
        ValidateSlot(slot);
        if (states[slot] != PartyMemberState.Exiting)
            throw new InvalidOperationException("只有 Exiting 槽可以完成退场。");
        states[slot] = PartyMemberState.Inactive;
    }

    /// <summary>把非当前槽标记为死亡，供顺序选择器后续跳过。</summary>
    public void MarkDead(int slot)
    {
        ValidateSlot(slot);
        if (slot == ActiveIndex)
            throw new InvalidOperationException("当前 Active 槽死亡切换需由后续队灭/强制换人规则处理。");
        states[slot] = PartyMemberState.Dead;
    }

    /// <summary>
    /// 用权威 Active 槽纠正本地预测；保留 Empty/Dead，清掉尚未确认的 Active/Exiting。
    /// </summary>
    public void SynchronizeActive(int activeSlot)
    {
        ValidateSlot(activeSlot);
        if (states[activeSlot] == PartyMemberState.Empty
            || states[activeSlot] == PartyMemberState.Dead)
        {
            throw new InvalidOperationException("权威 Active 槽必须是可用角色。");
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i] == PartyMemberState.Active
                || states[i] == PartyMemberState.Exiting)
            {
                states[i] = PartyMemberState.Inactive;
            }
        }
        states[activeSlot] = PartyMemberState.Active;
        ActiveIndex = activeSlot;
    }

    /// <summary>检查槽位索引，避免静默写坏协调器状态。</summary>
    void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= states.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
