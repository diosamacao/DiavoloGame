using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单个远端连接创建的 ACT 权威阵容；网络状态仍归 ServerSession。</summary>
public sealed class ActGameGuest
{
    readonly ActGameGuestMember[] _members;

    /// <summary>保存 Guest 阵容，供输入路由、复制 Capture 与断线清理。</summary>
    internal ActGameGuest(
        NetConnectionId connectionId,
        RemotePlayerSeat seat,
        PartyCombatCoordinator coordinator,
        ActGameGuestMember[] members)
    {
        ConnectionId = connectionId;
        Seat = seat ?? throw new ArgumentNullException(nameof(seat));
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _members = members ?? throw new ArgumentNullException(nameof(members));
    }

    /// <summary>创建该 Guest 的网络连接。</summary>
    public NetConnectionId ConnectionId { get; }
    /// <summary>供 App 玩家花名册与 Transform 生命周期使用的远端 Seat。</summary>
    public RemotePlayerSeat Seat { get; }
    /// <summary>当前接收输入的权威 CharacterActor。</summary>
    public CharacterActor Actor => ActiveMember?.Actor;
    /// <summary>纯规则阵容协调器。</summary>
    public PartyCombatCoordinator Coordinator { get; }
    /// <summary>按 Loadout 槽序对齐的权威成员。</summary>
    internal IReadOnlyList<ActGameGuestMember> Members => _members;
    /// <summary>当前 Active 成员；非法状态下返回 null。</summary>
    internal ActGameGuestMember ActiveMember =>
        Coordinator.ActiveIndex >= 0 && Coordinator.ActiveIndex < _members.Length
            ? _members[Coordinator.ActiveIndex]
            : null;
    /// <summary>已灌入权威输入缓冲的最新客户端 FrameHint。</summary>
    public long LastAppliedFrameHint { get; set; }

    /// <summary>本逻辑步真正灌入的最新 Hint；无新命令时下行 0。</summary>
    public long AppliedHintThisTick { get; set; }

    /// <summary>死亡等待与队灭期间关闭座位输入；命令 ACK 仍由 Dedicated 正常推进。</summary>
    public bool CanAcceptGameplayInput =>
        Coordinator.CanAcceptGameplayInput
        && Actor != null
        && !Actor.Vitality.IsDead;

    /// <summary>按 Cue/点数裁定切人并应用 DualPresence 或 InstantReplace。</summary>
    public bool TryResolveSwitch()
    {
        if (!CanAcceptGameplayInput)
            return false;
        if (!Coordinator.TryResolveSwitch(BuildAssistQuery(), out PartySwitchCommand command))
            return false;

        ActGameGuestMember from = _members[command.FromSlot];
        ActGameGuestMember to = _members[command.ToSlot];
        if (command.Presentation == PartySwitchPresentation.InstantReplace)
        {
            from.Actor.PartyLifecycle.SetState(PartyMemberState.Inactive);
            to.Actor.PartyLifecycle.SetState(PartyMemberState.Active);
            if (command.CueOwnerId.IsValid)
                to.Actor.ForceSelectTarget(command.CueOwnerId);
            AssistCue cue = default;
            CombatWorldController world = CombatWorldController.Current;
            SimulationHost host = world != null ? world.SimulationHost : null;
            host?.AssistCues.TryGetActive(command.CueOwnerId, out cue);
            to.Actor.PartyLifecycle.PlaceForAssistSwitchFrom(
                from.Actor,
                in cue,
                PartySwitchApplication.UsesEvadeOffset(command.Kind));
            to.Actor.PartyLifecycle.QueueExternalIntent(
                PartySwitchApplication.ToIncomingIntent(command.Kind));
        }
        else
        {
            to.Actor.PartyLifecycle.PlaceForNormalSwitchFrom(from.Actor);
            from.Actor.PartyLifecycle.BeginExit();
            to.Actor.PartyLifecycle.SetState(PartyMemberState.Active);
            to.Actor.PartyLifecycle.QueueExternalIntent(GameplayIntentType.SwitchIn);
        }

        Seat.Bind(to.Actor, to.Root);
        return true;
    }

    /// <summary>
    /// AfterLogicStep 推进死亡门禁，并原子隐藏死亡槽、激活下一槽或保留队灭终态。
    /// 该路径只允许 Dead→Active，不进入 Exiting，也不请求 SwitchOut。
    /// </summary>
    public bool ProcessDeathCloseoutAfterLogicStep()
    {
        ActGameGuestMember active = ActiveMember;
        CharacterActor actor = active?.Actor;
        if (actor == null
            || !Coordinator.TryResolveActiveDeath(
                actor.Vitality.IsDead,
                actor.DeathSequenceComplete,
                actor.DeathActionTotalFrames,
                out PartyDeathCloseout closeout))
        {
            return false;
        }

        ActGameGuestMember dead = _members[closeout.FromSlot];
        dead.Actor.PartyLifecycle.SetState(PartyMemberState.Dead);
        if (closeout.PartyWiped)
            return true;

        ActGameGuestMember incoming = _members[closeout.ToSlot];
        incoming.Actor.PartyLifecycle.PlaceForNormalSwitchFrom(dead.Actor);
        incoming.Actor.PartyLifecycle.SetState(PartyMemberState.Active);
        incoming.Actor.PartyLifecycle.QueueExternalIntent(GameplayIntentType.SwitchIn);
        Seat.Bind(incoming.Actor, incoming.Root);
        return true;
    }

    /// <summary>读 Host CueBoard 与当前突击武装；锁定目标优先。</summary>
    PartyAssistResolveQuery BuildAssistQuery()
    {
        CharacterActor active = Actor;
        bool followUp = active != null && active.Numeric.Flags.HasAssistFollowUp;
        SimActorId preferred = default;
        if (active != null && active.TryGetSelectedTarget(out ITargetable target))
            preferred = target.SimulationId;

        CombatWorldController world = CombatWorldController.Current;
        SimulationHost host = world != null ? world.SimulationHost : null;
        if (host != null && host.AssistCues.TryGetActive(preferred, out AssistCue cue))
            return new PartyAssistResolveQuery(true, cue.Kind, cue.RequiresRanged, followUp, cue.OwnerId);

        return new PartyAssistResolveQuery(false, AssistCueKind.Gold, false, followUp);
    }

    /// <summary>帧末把已收完当前动作的 Exiting 成员转入后台。</summary>
    public void CompleteFinishedExits()
    {
        for (int i = 0; i < _members.Length; i++)
        {
            ActGameGuestMember member = _members[i];
            if (member?.Actor == null
                || member.Actor.PartyLifecycle.State != PartyMemberState.Exiting)
                continue;
            if (!member.Actor.PartyLifecycle.IsExitReady)
                continue;

            member.Actor.PartyLifecycle.CompleteExit();
            Coordinator.CompleteExit(i);
        }
    }

    /// <summary>复制按槽序稳定身份；空槽写 Invalid。</summary>
    public SimActorId[] CopyPartyActorIds()
    {
        var ids = new SimActorId[_members.Length];
        for (int i = 0; i < _members.Length; i++)
            ids[i] = _members[i]?.Actor?.SimulationId ?? SimActorId.Invalid;
        return ids;
    }

    /// <summary>按槽复制权威 PartyMemberState flags，供 V2 Meta 纠正客户端阵容。</summary>
    public int[] CopyPartyFlags()
    {
        var flags = new int[_members.Length];
        for (int i = 0; i < flags.Length; i++)
            flags[i] = PartyReplicationPacking.WithMemberState(0, Coordinator.States[i]);
        return flags;
    }
}
