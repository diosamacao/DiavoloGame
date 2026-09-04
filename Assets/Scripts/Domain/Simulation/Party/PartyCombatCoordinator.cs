using System;
using System.Collections.Generic;

/// <summary>维护单座位阵容槽状态，并把单键输入裁定为普通切或极限支援命令。</summary>
public sealed class PartyCombatCoordinator
{
    const int MaxMembers = 3;
    readonly PartyMemberState[] states;
    readonly CharacterAssistStyle[] _assistStyles;

    /// <summary>按 Loadout 槽占用表创建阵容状态；槽数必须为 1～3，开局槽必须有人。</summary>
    public PartyCombatCoordinator(
        IReadOnlyList<bool> occupiedSlots,
        int startingSlot,
        IReadOnlyList<CharacterAssistStyle> assistStyles = null)
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
        _assistStyles = new CharacterAssistStyle[occupiedSlots.Count];
        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            states[i] = occupiedSlots[i] ? PartyMemberState.Inactive : PartyMemberState.Empty;
            _assistStyles[i] = assistStyles != null && i < assistStyles.Count
                ? assistStyles[i]
                : CharacterAssistStyle.MeleeParry;
        }

        ActiveIndex = startingSlot;
        states[startingSlot] = PartyMemberState.Active;
        AssistPoints = new PartyAssistPoints();
    }

    /// <summary>当前接收玩法输入的槽位。</summary>
    public int ActiveIndex { get; private set; }

    /// <summary>队共享支援点；扣费只在本类裁定成功时发生。</summary>
    public PartyAssistPoints AssistPoints { get; }

    /// <summary>供 HUD、测试与后续 Actor 宿主读取的槽状态。</summary>
    public IReadOnlyList<PartyMemberState> States => states;

    /// <summary>
    /// 解析一次无 Cue 的普通切人：旧槽进入 Exiting，新槽立即 Active，并输出 DualPresence。
    /// </summary>
    public bool TryResolveSwitchIn(out PartySwitchCommand command) =>
        TryResolveSwitch(PartyAssistResolveQuery.None, out command);

    /// <summary>
    /// 按 Cue / 点数 / 上场 AssistStyle 裁定切人。突击武装中拒绝。
    /// InstantReplace 旧槽当帧 Inactive，不进 Exiting。
    /// </summary>
    public bool TryResolveSwitch(in PartyAssistResolveQuery query, out PartySwitchCommand command)
    {
        command = default;
        if (query.AssistFollowUpArmed)
            return false;
        if (!PartySlotSelector.TryFindNext(ActiveIndex, states, out int nextIndex))
            return false;

        CharacterAssistStyle incomingStyle = _assistStyles[nextIndex];
        if (!query.HasCue)
        {
            ApplyDualPresence(nextIndex, out command);
            return true;
        }

        AssistCueKind effective = query.CueKind;
        if (effective == AssistCueKind.Gold && !AssistPoints.CanSpendAssist)
            effective = AssistCueKind.Red;
        if (effective == AssistCueKind.Gold && query.RequiresRanged
            && incomingStyle != CharacterAssistStyle.RangedEvade)
        {
            effective = AssistCueKind.Red;
        }

        if (effective == AssistCueKind.Gold)
        {
            if (!AssistPoints.TrySpendAssist())
                return false;

            PartySwitchKind kind = incomingStyle == CharacterAssistStyle.RangedEvade
                ? PartySwitchKind.AssistEvade
                : PartySwitchKind.AssistParry;
            ApplyInstantReplace(
                nextIndex,
                kind,
                PartyAssistPoints.AssistCost,
                query.CueOwnerId,
                out command);
            return true;
        }

        ApplyInstantReplace(
            nextIndex,
            PartySwitchKind.SwitchPerfectDodge,
            spendAssistPoints: 0,
            query.CueOwnerId,
            out command);
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

    void ApplyDualPresence(int nextIndex, out PartySwitchCommand command)
    {
        int previousIndex = ActiveIndex;
        states[previousIndex] = PartyMemberState.Exiting;
        states[nextIndex] = PartyMemberState.Active;
        ActiveIndex = nextIndex;
        command = new PartySwitchCommand(
            previousIndex,
            nextIndex,
            PartySwitchKind.SwitchIn,
            PartySwitchPresentation.DualPresence);
    }

    void ApplyInstantReplace(
        int nextIndex,
        PartySwitchKind kind,
        int spendAssistPoints,
        SimActorId cueOwnerId,
        out PartySwitchCommand command)
    {
        int previousIndex = ActiveIndex;
        states[previousIndex] = PartyMemberState.Inactive;
        states[nextIndex] = PartyMemberState.Active;
        ActiveIndex = nextIndex;
        command = new PartySwitchCommand(
            previousIndex,
            nextIndex,
            kind,
            PartySwitchPresentation.InstantReplace,
            spendAssistPoints,
            cueOwnerId);
    }

    void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= states.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
