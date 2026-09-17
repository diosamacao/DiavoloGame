using System;
using System.Collections.Generic;

/// <summary>维护单座位阵容槽状态，并把单键输入裁定为普通切或极限支援命令。</summary>
public sealed class PartyCombatCoordinator
{
    const int MaxMembers = 3;
    readonly PartyMemberState[] states;
    readonly CharacterAssistStyle[] _assistStyles;
    readonly PartyDeathSwitchGate _deathSwitchGate = new();

    /// <summary>按 Loadout 槽占用表创建阵容状态；槽数必须为 1～3，开局槽必须有人。assistPoints 全 0 用首版默认。</summary>
    public PartyCombatCoordinator(
        IReadOnlyList<bool> occupiedSlots,
        int startingSlot,
        IReadOnlyList<CharacterAssistStyle> assistStyles = null,
        PartyAssistPointSettings assistPoints = default)
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
        AssistPoints = new PartyAssistPoints(assistPoints);
    }

    /// <summary>当前接收玩法输入的槽位。</summary>
    public int ActiveIndex { get; private set; }

    /// <summary>最后一名成员死亡收尾已提交；V2 PartyWiped Meta 接线前由权威口袋持有。</summary>
    public bool IsPartyWiped { get; private set; }

    /// <summary>当前阵容是否仍允许座位级玩法输入。</summary>
    public bool CanAcceptGameplayInput =>
        !IsPartyWiped
        && ActiveIndex >= 0
        && ActiveIndex < states.Length
        && states[ActiveIndex] == PartyMemberState.Active;

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
        if (!CanAcceptGameplayInput || query.AssistFollowUpArmed)
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
                AssistPoints.Cost,
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

    /// <summary>
    /// 帧末推进 Active 死亡门禁；打开时原子写 Dead，并激活下一存活槽或提交队灭。
    /// 死亡路径不会产生 Exiting，也不会请求 SwitchOut。
    /// </summary>
    public bool TryResolveActiveDeath(
        bool isDead,
        bool deathSequenceComplete,
        int deathActionTotalFrames,
        out PartyDeathCloseout closeout)
    {
        closeout = default;
        if (IsPartyWiped || ActiveIndex < 0 || ActiveIndex >= states.Length)
            return false;

        int deadSlot = ActiveIndex;
        if (!_deathSwitchGate.Advance(
                deadSlot,
                isDead,
                deathSequenceComplete,
                deathActionTotalFrames))
        {
            return false;
        }

        bool hasNext = TryFindNextSurvivingMember(deadSlot, out int nextSlot);
        states[deadSlot] = PartyMemberState.Dead;
        if (hasNext)
        {
            // 死亡接替可召回仍在退场动作中的存活槽，不能把 Exiting 误判成全队阵亡。
            states[nextSlot] = PartyMemberState.Active;
            ActiveIndex = nextSlot;
        }
        else
        {
            IsPartyWiped = true;
            nextSlot = -1;
        }

        closeout = new PartyDeathCloseout(deadSlot, nextSlot);
        return true;
    }

    /// <summary>按槽序选择任意存活成员；死亡路径允许取消 Exiting 并重新激活。</summary>
    bool TryFindNextSurvivingMember(int fromSlot, out int nextSlot)
    {
        for (int offset = 1; offset < states.Length; offset++)
        {
            int candidate = (fromSlot + offset) % states.Length;
            PartyMemberState state = states[candidate];
            if (state == PartyMemberState.Inactive || state == PartyMemberState.Exiting)
            {
                nextSlot = candidate;
                return true;
            }
        }

        nextSlot = -1;
        return false;
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
        IsPartyWiped = false;
        _deathSwitchGate.Reset();
    }

    /// <summary>从 Owner 角色快照同步单槽 PartyState；Active 仍由既有 ActiveSlot 原子纠正。</summary>
    public void SynchronizeMemberState(int slot, PartyMemberState state)
    {
        ValidateSlot(slot);
        if (state == PartyMemberState.Active)
        {
            SynchronizeActive(slot);
            return;
        }
        if (states[slot] == PartyMemberState.Empty && state != PartyMemberState.Empty)
            throw new InvalidOperationException("空槽不能从角色快照变成阵容成员。");

        states[slot] = state;
        if (slot == ActiveIndex && state == PartyMemberState.Dead)
        {
            IsPartyWiped = !TryFindNextSurvivingMember(slot, out _);
            _deathSwitchGate.Reset();
        }
    }

    /// <summary>应用 V2 Meta 队灭终态；保留各槽 flags，只关闭 Active 与玩法输入。</summary>
    public void SynchronizePartyWiped()
    {
        IsPartyWiped = true;
        ActiveIndex = -1;
        _deathSwitchGate.Reset();
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
