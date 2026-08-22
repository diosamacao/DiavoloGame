using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// W11 FakeActionGame：可移动实体 + Owner 预测 + Observer 插值，不引用 ACT Character。
/// </summary>
public sealed class FakeActionGameLoopbackTests
{
    const ushort SchemaId = 7;
    const int NearEnemy = 2;
    const int FarEnemy = 3;

    /// <summary>Owner 预测即时前进，权威对照后位置与预测一致。</summary>
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
    }

    /// <summary>远端按 Timeline 取样，旧 Tick 不能回滚。</summary>
    [Test]
    public void Observer_SamplesTimeline_AndRejectsOlderTick()
    {
        var game = new FakeGame();
        game.StepAuthority(nearDeltaX: 50);
        game.StepAuthority(nearDeltaX: 50);
        Assert.That(game.ObserverTimeline.LatestTick, Is.EqualTo(2));
        Assert.That(game.ObserverTimeline.TryPush(1, new FakePose(NearEnemy, 0, 0)), Is.False);
    }

    /// <summary>兴趣半径外的敌人不 Spawn 给该连接。</summary>
    [Test]
    public void Relevancy_DoesNotSendFarEnemy()
    {
        var game = new FakeGame();
        game.StepAuthority();
        Assert.That(game.Client.Registry.TryGet(new NetEntityId(1), out _), Is.True);
        Assert.That(game.Client.Registry.TryGet(new NetEntityId(NearEnemy), out _), Is.True);
        Assert.That(game.Client.Registry.TryGet(new NetEntityId(FarEnemy), out _), Is.False);
    }

    /// <summary>12 个静止敌人在 Compact 策略下 60 Tick 的 Update 字节远低于每 Tick 全量。</summary>
    [Test]
    public void Compact_TenPlusIdleActors_BeatsFullRateBytes()
    {
        var compact = new ReplicationServer();
        var full = new ReplicationServer();
        var idle = new List<ReplicationEntityState>();
        for (int i = 1; i <= 12; i++)
            idle.Add(PoseState(i, 0, 0));

        compact.BuildFrame(new NetTick(1), idle, Array.Empty<byte>(), ReplicationBuildOptions.Compact);
        full.BuildFrame(new NetTick(1), idle, Array.Empty<byte>());

        int compactBytes = 0;
        int fullBytes = 0;
        for (int tick = 2; tick <= 61; tick++)
        {
            compactBytes += compact.BuildFrame(
                new NetTick(tick),
                idle,
                Array.Empty<byte>(),
                ReplicationBuildOptions.Compact).Updates.Length;
            fullBytes += full.BuildFrame(
                new NetTick(tick),
                idle,
                Array.Empty<byte>(),
                new ReplicationBuildOptions(
                    skipUnchanged: false,
                    maxUpdateBytes: 0,
                    snapshotIntervalTicks: 1,
                    preferredEntity: default,
                    forceFull: false)).Updates.Length;
        }

        Assert.That(compactBytes, Is.Zero);
        Assert.That(fullBytes, Is.EqualTo(12 * 60));
    }

    static ReplicationEntityState PoseState(int id, int x, int z) =>
        new ReplicationEntityState(
            new NetEntityId(id),
            new NetArchetypeId(1),
            SchemaId,
            FakePoseSchema.Encode(new FakePose(id, x, z)));

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
        public ushort SchemaId => FakeActionGameLoopbackTests.SchemaId;

        public byte[] Encode(object state) =>
            FakePoseSchema.Encode((FakePose)state);

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

        public int MeasureError(in int authority, in int predicted)
        {
            int delta = predicted - authority;
            return delta < 0 ? -delta : delta;
        }
    }

    /// <summary>最小房间：1 Owner + 近敌 + 远敌。</summary>
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
            Client.Spawned += OnSpawnOrUpdate;
            Client.Updated += record => OnSpawnOrUpdate(
                new SpawnRecord(record.EntityId, new NetArchetypeId(1), record.SchemaId, record.Payload));
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

        public void ReconcileOwner()
        {
            OwnerCoordinator.ReceiveAuthority(
                _tick,
                _ownerX,
                PredictionCorrectionPolicy.AcknowledgeOnly);
        }

        public void StepAuthority(int ownerDeltaX = 0, int nearDeltaX = 0)
        {
            _tick++;
            _ownerX += ownerDeltaX;
            _nearX += nearDeltaX;
            var relevant = new List<ReplicationEntityState>
            {
                PoseState(1, _ownerX, 0),
                PoseState(NearEnemy, _nearX, 0),
            };
            ReplicationFrame frame = _server.BuildFrame(
                new NetTick(_tick),
                relevant,
                Array.Empty<byte>(),
                ReplicationBuildOptions.Compatible.WithPreferred(new NetEntityId(1)));
            Client.ApplyFrame(frame);
        }

        void OnSpawnOrUpdate(SpawnRecord record)
        {
            FakePose pose = FakePoseSchema.Decode(record.Payload);
            if (pose.Id == NearEnemy)
                _observer.TryPush(_tick, pose);
        }
    }
}
