using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>客户端 Owner 协调器：独占输入采样、命令冗余、本机预测与权威阵容纠正。</summary>
public sealed class OwnerPredictionCoordinator
{
    readonly CombatWorldController _world;
    readonly ActOwnerReplicationAdapter _owner;
    readonly ReplicatedFeedbackCoordinator _feedback;
    readonly List<ClientCommand> _recentCommands = new();
    PlayerController _localPlayer;
    SessionJoinAccept _accept;
    InputFrameBuffer _inputFrames;
    SimActorId[] _partyActorIds = Array.Empty<SimActorId>();
    long _predictFrame;
    InputFrame _pendingInput;
    bool _hasPendingStep;
    bool _loggedPredictOpen;

    /// <summary>创建 Owner 预测协调器并绑定反馈表现端口。</summary>
    public OwnerPredictionCoordinator(
        CombatWorldController world,
        ActOwnerReplicationAdapter owner,
        ReplicatedFeedbackCoordinator feedback)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
    }

    /// <summary>最近完整上行命令消息字节；尚未发送时为 -1。</summary>
    public int LastCommandBytes { get; private set; } = -1;

    /// <summary>Owner 最近权威生命值。</summary>
    public int SelfHealthMilli => _owner.SelfHealthMilli;

    /// <summary>Owner 尚未确认的动作与位移预测总数。</summary>
    public int PendingCount => _owner.PendingCount;

    /// <summary>走跑 Restore 次数。</summary>
    public int LocomotionSnapCount => _owner.LocomotionSnapCount;

    /// <summary>走跑 Replay 命令累计。</summary>
    public int LocomotionReplayCount => _owner.LocomotionReplayCount;

    /// <summary>权威快照已建立本机预测驱动。</summary>
    public bool CanPredict => _owner.CanPredict;

    /// <summary>本机阵容稳定 ActorId；Join Meta 前仅含 Session Owner。</summary>
    public IReadOnlyList<SimActorId> PartyActorIds => _partyActorIds;

    /// <summary>Owner 输入历史，Meta 绑定每槽 SimulationId 时复用。</summary>
    public InputFrameBuffer InputFrames
    {
        get
        {
            EnsureInputBuffer();
            return _inputFrames;
        }
    }

    /// <summary>Session Join 后初始化 Owner 身份、输入历史与预测时钟。</summary>
    public void BeginSession(in SessionJoinAccept accept, PlayerController localPlayer)
    {
        _accept = accept;
        _localPlayer = localPlayer;
        _predictFrame = accept.AuthorityTick.Value;
        EnsureInputBuffer();
        var sessionOwner = new SimActorId(accept.EntityId.Value);
        _owner.BeginSession(sessionOwner, _inputFrames);
        _partyActorIds = new[] { sessionOwner };
        _recentCommands.Clear();
        _feedback.BeginSession(_partyActorIds, _localPlayer);
        _loggedPredictOpen = false;
    }

    /// <summary>渲染帧采样下一预测帧输入，并合并按钮边沿。</summary>
    public void SampleRenderInput()
    {
        if (_localPlayer?.InputSampler == null || !_accept.EntityId.IsValid)
            return;
        EnsureInputBuffer();
        var actorId = new SimActorId(_accept.EntityId.Value);
        InputFrame sample = _localPlayer.InputSampler.Sample(_predictFrame + 1, actorId);
        _inputFrames.MergeLocalSample(in sample);
    }

    /// <summary>解析下一预测帧并编码冗余命令；调用方发送成功后再调用 StepPrediction。</summary>
    public bool TryBuildCommand(out byte[] commandBody)
    {
        commandBody = null;
        _hasPendingStep = false;
        if (_localPlayer?.InputSampler == null)
            return false;

        EnsureInputBuffer();
        _predictFrame++;
        var actorId = new SimActorId(_accept.EntityId.Value);
        InputFrame input = _inputFrames.ResolveLocal(_predictFrame, actorId);
        _inputFrames.TrimBefore(_predictFrame - 32);
        var command = new ClientCommand(_predictFrame, _accept.PlayerId.Value, in input);
        RememberCommand(in command);
        commandBody = RoomCodec.WriteClientCommandBatch(_recentCommands);
        LastCommandBytes = commandBody.Length + 2;
        _pendingInput = input;
        _hasPendingStep = true;
        return true;
    }

    /// <summary>命令正文发送后推进本机阵容，并记录 Owner ACK/Replay 历史。</summary>
    public void StepPrediction()
    {
        if (!_hasPendingStep)
            return;
        _hasPendingStep = false;
        CharacterActor actor = _localPlayer?.Actor;
        if (!_owner.CanPredict || actor == null)
            return;

        float fixedDeltaSeconds = _world.SimulationHost.FixedDeltaSeconds;
        _localPlayer.Party.StepPrediction(_predictFrame, fixedDeltaSeconds, in _pendingInput);
        actor = _localPlayer.Actor;
        int activeSlot = _localPlayer.Party.ActiveSlot;
        if (activeSlot >= 0 && activeSlot < _partyActorIds.Length)
            _owner.SetActiveOwnerActor(_partyActorIds[activeSlot]);
        _feedback.PresentPredictedHitStop(actor, _localPlayer);
        _feedback.ResolveAutonomousSoftBody(actor);
        _owner.RecordAutonomous(actor, _predictFrame, in _pendingInput);
    }

    /// <summary>应用 Party Meta，并按固定顺序绑定身份、同步槽状态、纠正 Active。</summary>
    public void ApplyAuthorityMeta(ActReplicationSnapshotMeta meta)
    {
        SimActorId[] ids = meta.PartyActorIds;
        int[] flags = meta.PartyFlags;
        if (_localPlayer?.Party == null || ids.Length != _localPlayer.Party.Actors.Count)
            throw new InvalidOperationException("V2 Meta 阵容与本机 Loadout 不一致。");

        _partyActorIds = ids;
        _localPlayer.Party.BindSimulationInput(ids, InputFrames);
        for (int i = 0; i < ids.Length; i++)
            _localPlayer.Party.SynchronizeMemberState(ids[i], flags[i]);
        if (meta.PartyWiped)
        {
            _localPlayer.Party.SynchronizePartyWiped();
        }
        else
        {
            _localPlayer.Party.SynchronizeActiveSlot(
                meta.ActivePartySlot,
                meta.LastAppliedClientFrameHint);
            _owner.SetActiveOwnerActor(ids[meta.ActivePartySlot]);
        }
        _feedback.UpdateOwnerRoster(_partyActorIds, true, _localPlayer);
    }

    /// <summary>把 Owner 槽快照中的 FlagsPacked 同步到本机阵容镜像。</summary>
    public void ApplyPartySnapshot(ActorReplicationSnapshot snapshot) =>
        _localPlayer?.Party?.SynchronizeMemberState(snapshot.ActorId, snapshot.FlagsPacked);

    /// <summary>应用当前 Active Owner 的权威快照与累计 ACK。</summary>
    public void ApplySelfSnapshot(
        in ActorReplicationSnapshot snapshot,
        long lastAppliedClientFrameHint) =>
        _owner.ApplySnapshot(_localPlayer, in snapshot, lastAppliedClientFrameHint);

    /// <summary>返回当前 Active Owner；队灭时退回 Session Owner 供幂等 Despawn 判断。</summary>
    public SimActorId ResolveCurrentOwnerId(ActReplicationSnapshotMeta meta)
    {
        if (meta != null && !meta.PartyWiped)
            return meta.PartyActorIds[meta.ActivePartySlot];
        return _accept.EntityId.IsValid
            ? new SimActorId(_accept.EntityId.Value)
            : SimActorId.Invalid;
    }

    /// <summary>首次开启预测时输出一次诊断日志。</summary>
    public void LogPredictionOpenedOnce(long authorityFrame)
    {
        if (_loggedPredictOpen || !_owner.CanPredict)
            return;
        _loggedPredictOpen = true;
        Debug.Log(
            $"ActClientRoomGameplay: Owner 预测已开闸 tick={authorityFrame} "
            + $"actor={_accept.EntityId.Value}。");
    }

    /// <summary>Recovery 时清空预测驱动，保留阵容身份供 Observer 排除自有实体。</summary>
    public void ResetForRecovery()
    {
        _owner.Reset();
        _loggedPredictOpen = false;
    }

    /// <summary>关闭房间时清空 Owner、命令冗余与待推进帧。</summary>
    public void Shutdown()
    {
        _owner.Reset();
        _recentCommands.Clear();
        _hasPendingStep = false;
    }

    /// <summary>保留最近若干命令，供下一应用包冗余重发。</summary>
    void RememberCommand(in ClientCommand command)
    {
        _recentCommands.Add(command);
        int max = ReplicationRoomProtocol.InputRedundancyCount;
        while (_recentCommands.Count > max)
            _recentCommands.RemoveAt(0);
    }

    void EnsureInputBuffer() => _inputFrames ??= new InputFrameBuffer();
}
