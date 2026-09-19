using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>W11 接入证明：纯 ACTNet V2 假游戏覆盖预测、观察者、兴趣与 10+ 实体带宽。</summary>
public sealed class FakeActionGameV2LoopbackTests
{
    const ushort SchemaId = 7;
    const int Owner = 1;
    const int NearEnemy = 2;
    const int FarEnemy = 3;
    const int BodyBudget = 512;

    /// <summary>Owner 无需等待权威即可预测；收到一致状态后只确认、不回滚。</summary>
    [Test]
    public void Owner_PredictsWithoutWaitingAuthority()
    {
        var game = new FakeGame();

        game.OwnerPredict(deltaX: 100);
        Assert.That(game.OwnerPredictedX, Is.EqualTo(100));

        game.StepAuthority(ownerDeltaX: 100);
        game.ReconcileOwner();

        Assert.That(game.OwnerPredictedX, Is.EqualTo(100));
        Assert.That(game.OwnerCoordinator.Metrics.SnapCount, Is.Zero);
        Assert.That(game.OwnerCoordinator.PendingCount, Is.Zero);
    }

    /// <summary>V2 快照进入观察者时间线并可在隔步样本间插值；旧 Tick 不得回滚。</summary>
    [Test]
    public void Observer_V2SnapshotsInterpolate_AndRejectOlderTick()
    {
        var game = new FakeGame();
        game.StepAuthority(nearDeltaX: 30);
        game.StepAuthority(nearDeltaX: 30);
        game.StepAuthority(nearDeltaX: 30);

        Assert.That(game.ObserverTimeline.LatestTick, Is.EqualTo(3));
        Assert.That(game.ObserverTimeline.TrySampleAt(
            2d,
            out long fromTick,
            out long toTick,
            out FakePose from,
            out FakePose to,
            out float alpha), Is.True);
        Assert.That(fromTick, Is.EqualTo(1));
        Assert.That(toTick, Is.EqualTo(3));
        Assert.That(from.X, Is.EqualTo(30));
        Assert.That(to.X, Is.EqualTo(90));
        Assert.That(alpha, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(game.ObserverTimeline.TryPush(2, new FakePose(NearEnemy, 0, 0)), Is.False);
    }

    /// <summary>Owner/玩家始终相关，兴趣半径外敌人不进入该连接的 V2 生命周期。</summary>
    [Test]
    public void Relevancy_DoesNotSpawnFarEnemy()
    {
        var game = new FakeGame();
        game.StepAuthority();

        Assert.That(game.Client.Registry.TryGet(new NetEntityId(Owner), out _), Is.True);
        Assert.That(game.Client.Registry.TryGet(new NetEntityId(NearEnemy), out _), Is.True);
        Assert.That(game.Client.Registry.TryGet(new NetEntityId(FarEnemy), out _), Is.False);
    }

    /// <summary>12 个静止实体在 60 Tick 内只做周期保底，正文显著小于每 Tick 全量 V2。</summary>
    [Test]
    public void Compact_TenPlusIdleActors_BeatsFullRateBytes()
    {
        var server = new ReplicationServer();
        var idle = new List<ReplicationEntityState>();
        var fullRecords = new EntityRecord[12];
        for (int i = 0; i < fullRecords.Length; i++)
        {
            ReplicationEntityState state = PoseState(i + 1, 0, 0);
            idle.Add(state);
            fullRecords[i] = new EntityRecord(state.EntityId, SchemaId, state.Payload);
        }
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), idle, Array.Empty<byte>(), BodyBudget, ReplicationBuildOptions.Compact));

        int compactBytes = 0;
        for (int tick = 2; tick <= 61; tick++)
        {
            ReplicationTickDelta delta = server.PrepareTickDelta(
                new NetTick(tick), idle, Array.Empty<byte>(), BodyBudget, ReplicationBuildOptions.Compact);
            compactBytes += SnapshotBytes(delta);
            CommitAll(server, delta);
        }

        int fullFrameBytes = ReplicationProtocolV2Codec.EncodeSnapshot(
            new ReplicationSnapshot(new NetTick(1), 0, 0, 1, fullRecords, Array.Empty<byte>()),
            BodyBudget).Length;
        int fullRateBytes = fullFrameBytes * 60;

        Assert.That(compactBytes, Is.GreaterThan(0), "MaxSilence 必须保底重发静止状态。");
        Assert.That(compactBytes, Is.LessThan(fullRateBytes / 10));
    }

    static ReplicationEntityState PoseState(int id, int x, int z) =>
        new(
            new NetEntityId(id),
            new NetArchetypeId(1),
            SchemaId,
            FakePoseSchema.Encode(new FakePose(id, x, z)));

    static int SnapshotBytes(ReplicationTickDelta delta)
    {
        int bytes = 0;
        for (int i = 0; i < delta.Packets.Length; i++)
            if (!delta.Packets[i].ReliableLifecycle)
                bytes += delta.Packets[i].Body.Length;
        return bytes;
    }

    static void CommitAll(ReplicationServer server, ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++)
            server.Commit(delta.Packets[i].Token);
    }

    readonly struct FakePose
    {
        public FakePose(int id, int x, int z)
        {
            Id = id;
            X = x;
            Z = z;
        }

        public int Id { get; }
        public int X { get; }
        public int Z { get; }
    }

    static class FakePoseSchema
    {
        public static byte[] Encode(in FakePose pose)
        {
            var writer = new NetBufferWriter(16);
            writer.WriteInt32(pose.Id);
            writer.WriteInt32(pose.X);
            writer.WriteInt32(pose.Z);
            return writer.ToArray();
        }

        public static FakePose Decode(byte[] payload)
        {
            var reader = new NetBufferReader(payload);
            var pose = new FakePose(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            reader.EnsureComplete();
            return pose;
        }
    }

    sealed class FakePoseReplicationSchema : IReplicationSchema
    {
        public ushort SchemaId => FakeActionGameV2LoopbackTests.SchemaId;
        public byte[] Encode(object state) => FakePoseSchema.Encode((FakePose)state);
        public object Decode(byte[] payload) => FakePoseSchema.Decode(payload);
    }

    sealed class LinearPoseModel : IPredictionModel<int, int>
    {
        public int X { get; private set; }
        public int Capture() => X;
        public void Restore(in int authorityState) => X = authorityState;
        public bool TrySimulate(in int command, in PredictionCorrectionPolicy policy)
        {
            X += command;
            return true;
        }
        public int MeasureError(in int authority, in int predicted) => Math.Abs(predicted - authority);
    }

    /// <summary>不引用 ACT Character 的最小 V2 房间：一个 Owner、一个近敌和一个远敌。</summary>
    sealed class FakeGame
    {
        readonly LinearPoseModel _model = new();
        readonly ReplicationServer _server = new();
        readonly SnapshotTimeline<FakePose> _observer = new();
        int _nearX;
        int _ownerX;
        int _tick;

        public FakeGame()
        {
            var schemas = new ReplicationSchemaRegistry();
            schemas.Register(new FakePoseReplicationSchema());
            Client = new ReplicationClient(schemas);
            OwnerCoordinator = new PredictionCoordinator<int, int>(_model);
            Client.Spawned += record => OnPose(record.EntityId, record.Payload, _tick);
            Client.Updated += (record, tick) => OnPose(record.EntityId, record.Payload, tick);
        }

        public ReplicationClient Client { get; }
        public PredictionCoordinator<int, int> OwnerCoordinator { get; }
        public SnapshotTimeline<FakePose> ObserverTimeline => _observer;
        public int OwnerPredictedX => _model.X;

        public void OwnerPredict(int deltaX)
        {
            _model.TrySimulate(deltaX, PredictionCorrectionPolicy.AcknowledgeOnly);
            OwnerCoordinator.Record(_tick + 1, deltaX, _model.X);
        }

        public void ReconcileOwner() => OwnerCoordinator.ReceiveAuthority(
            _tick,
            _ownerX,
            PredictionCorrectionPolicy.AcknowledgeOnly);

        public void StepAuthority(int ownerDeltaX = 0, int nearDeltaX = 0)
        {
            _tick++;
            _ownerX += ownerDeltaX;
            _nearX += nearDeltaX;
            var candidates = new[]
            {
                PoseState(Owner, _ownerX, 0),
                PoseState(NearEnemy, _nearX, 0),
                PoseState(FarEnemy, 50000, 0),
            };
            var relevant = new List<ReplicationEntityState>();
            for (int i = 0; i < candidates.Length; i++)
            {
                ReplicationEntityState state = candidates[i];
                FakePose pose = FakePoseSchema.Decode(state.Payload);
                bool isOwner = pose.Id == Owner;
                bool isPlayer = pose.Id == Owner;
                if (ReplicationInterest.IsRelevant(
                        isOwner,
                        isPlayer,
                        pose.X - _ownerX,
                        pose.Z,
                        ReplicationInterest.DefaultRadiusMm))
                {
                    relevant.Add(state);
                }
            }

            ReplicationTickDelta delta = _server.PrepareTickDelta(
                new NetTick(_tick),
                relevant,
                Array.Empty<byte>(),
                BodyBudget,
                ReplicationBuildOptions.Compact.WithPreferred(new NetEntityId(Owner)));
            for (int i = 0; i < delta.Packets.Length; i++)
            {
                PreparedReplicationPacket packet = delta.Packets[i];
                if (packet.ReliableLifecycle)
                    Client.ApplyLifecycle(ReplicationProtocolV2Codec.DecodeLifecycle(packet.Body));
                else
                    Client.ApplySnapshot(ReplicationProtocolV2Codec.DecodeSnapshot(packet.Body));
                _server.Commit(packet.Token);
            }
        }

        void OnPose(NetEntityId entityId, byte[] payload, long tick)
        {
            if (entityId.Value != NearEnemy)
                return;
            FakePose pose = FakePoseSchema.Decode(payload);
            _observer.TryPush(tick, pose);
        }
    }
}
