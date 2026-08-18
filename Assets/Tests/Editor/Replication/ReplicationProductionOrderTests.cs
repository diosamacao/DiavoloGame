using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>冻结薄 Room、ACT Gameplay Facade 与 Simulation 的生产调用顺序。</summary>
public sealed class ReplicationProductionOrderTests
{
    /// <summary>Host 必须先 Poll/消费输入，再于权威步末 Capture、构帧并发送。</summary>
    [Test]
    public void HostFrame_ProductionSource_PreservesReceiveStepCaptureSendOrder()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomHost.cs");
        string gameplay = ReadScript("App/Networking/Services/ActHostRoomGameplay.cs");
        string authority = ReadScript("App/Networking/Adapters/ActAuthorityReplicationAdapter.cs");
        string gameSession = ReadScript("App/Networking/Adapters/ActGameSessionHandler.cs");
        string simulation = ReadScript("App/Controllers/Gameplay/SimulationHost.cs");

        string roomUpdate = Slice(room, "    void Update()", "    void OnDisable()");
        AssertInOrder(
            roomUpdate,
            "_session.Poll(NowMs());",
            "_gameplay?.DrainPlayerRequests(_session);",
            "_gameplay?.DrainApplicationMessages(_session);");

        string applyCommands = Slice(
            authority,
            "    public ActAuthorityInputApplyResult ApplyGuestCommands(",
            "    public void CaptureAuthorityActors(");
        AssertInOrder(
            applyCommands,
            "currentFrame + 1",
            "RoomRemoteInputMerge.TryMergeUnapplied(",
            "buffer.Set(in merged);",
            "new ActAuthorityInputApplyResult(true, newestHint);");

        string simulationStep = Slice(
            simulation,
            "    public void StepOnce()",
            "    void Update()");
        AssertInOrder(
            simulationStep,
            "_combatHits.BeginFrame(",
            "_world.Step();",
            "_combatHits.ResolveBeforePostCombat(",
            "_world.ResolvePostCombat();",
            "_combatHits.CompleteFrame(",
            "CommitEnemyLifecycle();",
            "SendEvent(SimulationLogicStepEvent.Instance)",
            "AfterLogicStep?.Invoke(",
            "_frameHits.Clear();");

        string buildFrame = Slice(
            gameplay,
            "    public bool TryBuildReplicationFrame(",
            "    public void OnSessionDisconnected(");
        AssertInOrder(
            buildFrame,
            "_contentPrefill.EnsureActionsReady();",
            "_authority.CaptureAuthorityActors(",
            "_authority.CopyHits(",
            "ActReplicationApplicationPayloadCodec.Encode(",
            "replication.BuildFrame(",
            "ReplicationFrameCodec.Encode(frame);");

        string roomAfterStep = Slice(
            room,
            "    void OnAfterLogicStep(long authorityFrame)",
            "    void OnSessionDisconnected(");
        AssertInOrder(
            roomAfterStep,
            "_gameplay.CopyGuestConnections(",
            "_gameplay.TryBuildReplicationFrame(",
            "_session.SendApplication(");

        string spawnGuest = Slice(
            gameplay,
            "    bool TryCreateGuest(",
            "    CharacterConfig ResolveJoinConfig()");
        AssertInOrder(
            spawnGuest,
            "_gameSession.TryCreateGuest(",
            "_replicationByConnection[request.ConnectionId] = new ReplicationServer();",
            "_guests.Add(request.ConnectionId, guest);",
            "session.AcceptPlayer(");

        string createGuest = Slice(
            gameSession,
            "    public bool TryCreateGuest(",
            "    public void DestroyGuest(");
        AssertInOrder(
            createGuest,
            "new GameObject(\"RemotePlayer\")",
            "CharacterActorFactory.Create(",
            "_services.RegisterCombatActor?.Invoke(",
            "_services.RegisterTarget?.Invoke(",
            "_services.RegisterPlayer?.Invoke(",
            "actor.Enable();",
            "host.RegisterPlayer(actor);",
            "guest = new ActGameGuest(");

        string destroyGuest = Slice(
            gameSession,
            "    public void DestroyGuest(",
            "public sealed class ActGameSessionServices");
        AssertInOrder(
            destroyGuest,
            "_services.UnregisterPlayer?.Invoke(",
            "_services.UnregisterTarget?.Invoke(",
            "_services.UnregisterCombatActor?.Invoke(",
            "host.Unregister(guest.Registration);",
            "guest.Reactions?.Dispose();",
            "guest.Actor?.Dispose();",
            "_services.DestroyGameObject?.Invoke(");

        Assert.That(authority, Does.Contain("_characterSchema.Capture("));
        Assert.That(authority, Does.Not.Contain("CharacterReplicationCapture"));
        Assert.That(gameSession, Does.Not.Contain("hostPlayer.Root"));
        Assert.That(gameplay, Does.Not.Contain("new Vector3(2f"));
    }

    /// <summary>Client 必须先收权威并采样；逻辑步内先发送命令，再推进 Autonomous 预测。</summary>
    [Test]
    public void ClientFrame_ProductionSource_PreservesReceiveSampleSendPredictOrder()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");
        string gameplay = ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");
        string owner = ReadScript("App/Networking/Adapters/ActOwnerReplicationAdapter.cs");
        string observer = ReadScript("App/Networking/Adapters/ActObserverReplicationAdapter.cs");

        string update = Slice(room, "    void Update()", "    void LateUpdate()");
        AssertInOrder(
            update,
            "_session.Poll(NowMs());",
            "SyncSessionState();",
            "DrainApplicationMessages();",
            "if (_joined && !_ended)",
            "_gameplay?.SampleRenderInput();");

        string roomAfterStep = Slice(
            room,
            "    void OnAfterLogicStep(long _)",
            "    void SyncSessionState()");
        AssertInOrder(
            roomAfterStep,
            "_gameplay.TryBuildCommand(out byte[] body)",
            "_session.SendApplication(",
            "_gameplay.StepPrediction();");

        string buildCommand = Slice(
            gameplay,
            "    public bool TryBuildCommand(",
            "    public void StepPrediction()");
        AssertInOrder(
            buildCommand,
            "_predictFrame++;",
            "_inputFrames.ResolveLocal(",
            "RememberCommand(in command);",
            "RoomCodec.WriteClientCommandBatch(",
            "_pendingPredictionInput = input;");

        string prediction = Slice(
            gameplay,
            "    public void StepPrediction()",
            "    public ActClientFrameApplyStatus ApplyReplicationFrame(");
        AssertInOrder(
            prediction,
            "actor.Step(",
            "actor.ResolvePostCombat(",
            "PresentPredictedHitStop(actor);",
            "ResolveAutonomousSoftBody(actor);",
            "_owner.RecordAutonomous(");

        string applyFrame = Slice(
            gameplay,
            "    public ActClientFrameApplyStatus ApplyReplicationFrame(",
            "    public void Render()");
        AssertInOrder(
            applyFrame,
            "ReplicationFrameCodec.Decode(body);",
            "_replicationClient.ApplyFrame(frame);",
            "ActReplicationApplicationPayloadCodec.Decode(",
            "_observer.ApplySpawns(",
            "_observer.ApplyUpdates(",
            "_observer.ApplyDespawns(",
            "_owner.ApplySnapshot(",
            "PlayReplicatedHits(application.Hits);");

        string applyOwner = Slice(
            owner,
            "    public void ApplySnapshot(",
            "    public void Reset()");
        AssertInOrder(
            applyOwner,
            "ApplyAuthorityHealthMilli(self.HealthMilli);",
            "_actionAck.Reconcile(",
            "_driver.Reconcile(",
            "ApplyAuthorityVitalityEdge(",
            "_driver?.SnapToSnapshot(in self);");

        string applyObserverSpawns = Slice(
            observer,
            "    public void ApplySpawns(",
            "    public void ApplyUpdates(");
        AssertInOrder(
            applyObserverSpawns,
            "DecodeRecord(",
            "_content.ResolveKind(",
            "CreateProxy(config);",
            "_proxies.Add(",
            "_registerTarget?.Invoke(proxy);",
            "proxy.ApplySnapshot(in snapshot);");

        string applyObserverDespawns = Slice(
            observer,
            "    public bool ApplyDespawns(",
            "    public bool TryGetProxy(");
        AssertInOrder(
            applyObserverDespawns,
            "_unregisterTarget?.Invoke(proxy);",
            "proxy.Dispose();",
            "_proxies.Remove(id);");
    }

    /// <summary>从 Assets 相对路径读取真实生产脚本。</summary>
    static string ReadScript(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, "Scripts", relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }

    /// <summary>截取两个唯一方法签名之间的源码。</summary>
    static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"缺少起始标记：{startMarker}");
        Assert.That(end, Is.GreaterThan(start), $"缺少结束标记：{endMarker}");
        return source.Substring(start, end - start);
    }

    /// <summary>要求所有关键标记按给定次序出现。</summary>
    static void AssertInOrder(string source, params string[] markers)
    {
        int cursor = 0;
        for (int i = 0; i < markers.Length; i++)
        {
            int index = source.IndexOf(markers[i], cursor, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"调用顺序缺失或逆序：{markers[i]}");
            cursor = index + markers[i].Length;
        }
    }
}
