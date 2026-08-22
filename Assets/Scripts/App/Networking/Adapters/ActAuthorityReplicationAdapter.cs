using System;
using System.Collections.Generic;

/// <summary>ACT 权威侧复制适配器：灌入远端输入，并把 Gameplay Actor 与命中映射为通用复制数据。</summary>
public sealed class ActAuthorityReplicationAdapter
{
    readonly ActContentRegistry _content;
    readonly ActCharacterSnapshotSchema _characterSchema;
    readonly List<EnemyController> _enemies = new();
    readonly List<ActorReplicationSnapshot> _snapshots = new();
    readonly List<ReplicationEntityState> _entityStates = new();

    /// <summary>创建绑定当前房间 ACT 内容目录的权威适配器。</summary>
    public ActAuthorityReplicationAdapter(ActContentRegistry content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _characterSchema = new ActCharacterSnapshotSchema(_content);
    }

    /// <summary>最近一次 Capture 生成的完整权威实体集；仅供同一逻辑步构建 ReplicationFrame。</summary>
    public IReadOnlyList<ReplicationEntityState> EntityStates => _entityStates;

    /// <summary>
    /// 按观察者兴趣复制子集：Owner 与玩家始终留下，敌人超出半径则不发。
    /// snapshots 与 entityStates 必须是同一次 Capture 的对齐列表。
    /// </summary>
    public void CopyRelevantStates(
        SimActorId observerId,
        int radiusMm,
        List<ReplicationEntityState> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        int originX = 0;
        int originZ = 0;
        bool hasOrigin = false;
        for (int i = 0; i < _snapshots.Count; i++)
        {
            if (_snapshots[i].ActorId != observerId)
                continue;
            originX = _snapshots[i].PosXMm;
            originZ = _snapshots[i].PosZMm;
            hasOrigin = true;
            break;
        }

        for (int i = 0; i < _entityStates.Count && i < _snapshots.Count; i++)
        {
            ActorReplicationSnapshot snapshot = _snapshots[i];
            bool isOwner = snapshot.ActorId == observerId;
            bool isPlayer = snapshot.Kind == ReplicationActorKind.Player;
            int dx = hasOrigin ? snapshot.PosXMm - originX : 0;
            int dz = hasOrigin ? snapshot.PosZMm - originZ : 0;
            if (!ReplicationInterest.IsRelevant(isOwner, isPlayer, dx, dz, radiusMm))
                continue;
            results.Add(_entityStates[i]);
        }
    }

    /// <summary>
    /// 合并尚未应用的远端命令并写入下一权威帧。
    /// NewestHint 跳过冗余；FirstAppliedHint 写入下行和解。无新命令时不覆盖下一帧已有输入。
    /// </summary>
    public ActAuthorityInputApplyResult ApplyGuestCommands(
        InputFrameBuffer buffer,
        long currentFrame,
        SimActorId actorId,
        ClientCommand[] commands,
        long lastAppliedHint)
    {
        if (buffer == null || !actorId.IsValid)
            return new ActAuthorityInputApplyResult(false, lastAppliedHint, lastAppliedHint);

        long targetFrame = currentFrame + 1;
        if (!RoomRemoteInputMerge.TryMergeUnapplied(
                commands,
                lastAppliedHint,
                targetFrame,
                actorId,
                out InputFrame merged,
                out long newestHint,
                out long firstAppliedHint))
        {
            return new ActAuthorityInputApplyResult(false, lastAppliedHint, lastAppliedHint);
        }

        if (buffer.TryGetExact(targetFrame, actorId, out InputFrame existing))
            merged = existing.MergeSample(in merged);

        buffer.Set(in merged);
        return new ActAuthorityInputApplyResult(true, newestHint, firstAppliedHint);
    }

    /// <summary>捕获全部权威 Guest 与运行中敌人；本机玩家不再作为特殊 Capture 入口。</summary>
    public void CaptureAuthorityActors(
        IReadOnlyList<ActGameGuest> guests,
        SimulationHost host)
    {
        _snapshots.Clear();
        _entityStates.Clear();
        if (guests != null)
        {
            for (int i = 0; i < guests.Count; i++)
            {
                ActGameGuest guest = guests[i];
                if (guest?.Actor == null)
                    continue;
                ActorReplicationSnapshot snapshot = _characterSchema.Capture(
                    guest.Actor,
                    ReplicationActorKind.Player);
                AddEntityState(in snapshot, guest.ArchetypeId);
            }
        }

        if (host == null)
            return;

        host.CopyEnemyControllers(_enemies);
        for (int i = 0; i < _enemies.Count; i++)
        {
            CharacterActor enemy = _enemies[i].Actor;
            if (enemy == null)
                continue;
            EnemyDefinition definition = _enemies[i].Definition;
            if (definition == null)
                throw new InvalidOperationException("运行中敌人缺少 EnemyDefinition，无法确定网络原型。");

            // 动态生成的 Definition 允许幂等登记；相同 key 指向不同资产时 Registry 会明确拒绝。
            NetArchetypeId archetypeId = _content.RegisterEnemy(definition);
            ActorReplicationSnapshot snapshot = _characterSchema.Capture(
                enemy,
                ReplicationActorKind.Enemy);
            AddEntityState(in snapshot, archetypeId);
        }
    }

    /// <summary>复制本帧权威命中，并从同一次 Capture 的攻击者快照补齐 ActionId。</summary>
    public ReplicatedHitEvent[] CopyHits(IReadOnlyList<ReplicatedHitEvent> frameHits)
    {
        if (frameHits == null || frameHits.Count == 0)
            return null;

        var copy = new ReplicatedHitEvent[frameHits.Count];
        for (int i = 0; i < frameHits.Count; i++)
        {
            ReplicatedHitEvent hit = frameHits[i];
            copy[i] = hit.WithActionId(ResolveHitActionId(in hit));
        }

        return copy;
    }

    /// <summary>记录命中映射所需快照，并编码为通用 ReplicationEntityState。</summary>
    void AddEntityState(
        in ActorReplicationSnapshot snapshot,
        NetArchetypeId archetypeId)
    {
        if (!snapshot.ActorId.IsValid)
            throw new InvalidOperationException("权威角色尚无有效 SimActorId，不能进入复制 full set。");

        _snapshots.Add(snapshot);
        bool urgent = snapshot.ActionId != 0
            || snapshot.VitalityEdge != VitalityReplicationEdge.None;
        _entityStates.Add(new ReplicationEntityState(
            new NetEntityId(snapshot.ActorId.Value),
            archetypeId,
            ActCharacterSnapshotSchema.Id,
            _characterSchema.Encode(in snapshot),
            urgent));
    }

    /// <summary>从同帧复制 full set 查找命中攻击者的 ActionId；缺失表示权威 Capture 不完整。</summary>
    int ResolveHitActionId(in ReplicatedHitEvent hit)
    {
        int attacker = hit.Key.AttackerId.Value;
        for (int i = 0; i < _snapshots.Count; i++)
        {
            if (_snapshots[i].ActorId.Value == attacker)
                return _snapshots[i].ActionId;
        }

        throw new InvalidOperationException(
            $"命中攻击者 {attacker} 不在本帧复制 full set，无法补写 ActionId。");
    }
}

/// <summary>远端命令灌入结果；Applied=false 时调用方必须保留原 Hint 状态。</summary>
public readonly struct ActAuthorityInputApplyResult
{
    /// <summary>创建输入灌入结果。firstAppliedHint 写入下行 appliedHint；newestHint 用于跳过冗余。</summary>
    public ActAuthorityInputApplyResult(bool applied, long newestHint, long firstAppliedHint)
    {
        Applied = applied;
        NewestHint = newestHint;
        FirstAppliedHint = firstAppliedHint;
    }

    /// <summary>本批命令是否实际写入权威输入缓冲。</summary>
    public bool Applied { get; }

    /// <summary>成功应用后的最新客户端 FrameHint；未应用时等于调用方传入值。</summary>
    public long NewestHint { get; }

    /// <summary>本批第一条新 Hint；客机按该帧预测位姿和解，避免用 newest 对压缩后的权威步。</summary>
    public long FirstAppliedHint { get; }
}
