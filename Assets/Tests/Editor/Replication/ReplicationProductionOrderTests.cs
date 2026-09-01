using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>冻结 Listen 组合、Dedicated Runtime、Client Runtime 与 Simulation 的生产调用顺序。</summary>
public sealed class ReplicationProductionOrderTests
{
    /// <summary>Listen 必须先发本机命令，再泵权威，再收同拍快照。</summary>
    [Test]
    public void ListenFrame_ProductionSource_PreservesLocalSendThenServerPollThenApply()
    {
        string listen = ReadScript("App/Controllers/Gameplay/ListenServerBootstrap.cs");
        string update = Slice(listen, "    void Update()", "    void LateUpdate()");
        AssertInOrder(
            update,
            "_local?.PollAndApply(nowMs);",
            "_local?.SampleRenderInput();",
            "_server.PeekAdvanceSteps(nowMs);",
            "_local?.SendCommandAndPredict();",
            "_server.Poll(nowMs);",
            "_local?.PollAndApply(nowMs);");
        Assert.That(listen, Does.Contain("DedicatedServerRuntime.TryStart("));
        Assert.That(listen, Does.Contain("new LocalClientRuntime("));
        Assert.That(listen, Does.Not.Contain("AfterLogicStep"));
    }

    /// <summary>权威灌入、步进、Guest 装配与 Capture 顺序仍由 Adapter / World / SessionHandler 承担。</summary>
    [Test]
    public void AuthorityFrame_ProductionSource_PreservesReceiveStepCaptureOrder()
    {
        string authority = ReadScript("App/Networking/Adapters/ActAuthorityReplicationAdapter.cs");
        string gameSession = ReadScript("App/Networking/Adapters/ActGameSessionHandler.cs");
        string simulation = ReadScript("App/Controllers/Gameplay/SimulationHost.cs");
        string world = ReadScript("App/Networking/Services/DedicatedAuthorityWorld.cs");

        string applyCommands = Slice(
            authority,
            "    public ActAuthorityInputApplyResult ApplyGuestCommands(",
            "    public void CaptureAuthorityActors(");
        AssertInOrder(
            applyCommands,
            "currentFrame + 1",
            "RoomRemoteInputMerge.TryMergeUnapplied(",
            "merged.WithoutButton(InputButton.SwitchCharacter)",
            "buffer.Set(in merged);",
            "new ActAuthorityInputApplyResult(true, newestHint, firstAppliedHint);");

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

        string accept = Slice(
            world,
            "    public bool TryAcceptPlayer(",
            "    public void ApplyCommands(");
        AssertInOrder(
            accept,
            "_gameSession.TryCreateGuest(",
            "_replicationByConnection[slot.ConnectionId] = new ReplicationServer();",
            "_guests[slot.ConnectionId] = guest;");

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
            "actor.Enable();",
            "host.RegisterPlayer(actor);",
            "guest = new ActGameGuest(",
            "seat.Bind(activeMember.Actor, activeMember.Root);",
            "_services.RegisterPlayer?.Invoke(");

        string destroyGuest = Slice(
            gameSession,
            "    public void DestroyGuest(",
            "public sealed class ActGameSessionServices");
        AssertInOrder(
            destroyGuest,
            "_services.UnregisterPlayer?.Invoke(",
            "_services.UnregisterTarget?.Invoke(",
            "_services.UnregisterCombatActor?.Invoke(",
            "host.Unregister(member.Registration);",
            "member.Reactions?.Dispose();",
            "member.Actor?.Dispose();",
            "_services.DestroyGameObject?.Invoke(");

        Assert.That(authority, Does.Contain("_characterSchema.Capture("));
        Assert.That(authority, Does.Not.Contain("CharacterReplicationCapture"));
        Assert.That(authority, Does.Not.Contain("ILocalPlayer local"));
        Assert.That(gameSession, Does.Not.Contain("hostPlayer.Root"));
        Assert.That(gameSession, Does.Contain("Seat.Bind(to.Actor, to.Root);"));
        Assert.That(world, Does.Not.Contain("new Vector3(2f"));
    }

    /// <summary>Dedicated 必须先灌命令再步进，步内 Capture 后由 Runtime 按连接发送。</summary>
    [Test]
    public void DedicatedFrame_ProductionSource_PreservesCommandStepCaptureSendOrder()
    {
        string runtime = ReadScript("App/Server/DedicatedServerRuntime.cs");
        string world = ReadScript("App/Networking/Services/DedicatedAuthorityWorld.cs");

        string poll = Slice(runtime, "    public void Poll(long nowMs)", "    public void RequestMatchEnd()");
        AssertInOrder(
            poll,
            "BeginPlayerTicks();",
            "_session.Poll(nowMs);",
            "DrainJoins();",
            "_authority.PublishImmediateReplication();",
            "DrainCommands();",
            "_authority.Advance(nowMs);",
            "FlushReplication();");

        string applyWorld = Slice(
            world,
            "    public void ApplyCommands(",
            "    public void RemovePlayer(");
        AssertInOrder(
            applyWorld,
            "ApplyGuestCommands(",
            "LastAppliedFrameHint = result.NewestHint",
            "AppliedHintThisTick = result.FirstAppliedHint");

        string afterStep = Slice(
            world,
            "    void OnAfterLogicStep(long authorityFrame)",
            "    void RememberHits(");
        AssertInOrder(
            afterStep,
            "_authority.CaptureAuthorityActors(",
            "RememberHits(",
            "ActReplicationApplicationPayloadCodec.Encode(",
            "replication.BuildFrame(",
            "ReplicationFrameCodec.Encode(frame)");
    }

    /// <summary>Client 必须先收权威并采样；逻辑步内先发送命令，再推进 Autonomous 预测。</summary>
    [Test]
    public void ClientFrame_ProductionSource_PreservesReceiveSampleSendPredictOrder()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");
        string runtime = ReadScript("App/Networking/Services/LocalClientRuntime.cs");
        string gameplay = ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");
        string owner = ReadScript("App/Networking/Adapters/ActOwnerReplicationAdapter.cs");
        string observer = ReadScript("App/Networking/Adapters/ActObserverReplicationAdapter.cs");

        string update = Slice(room, "    void Update()", "    void LateUpdate()");
        AssertInOrder(
            update,
            "_runtime.PollAndApply(NowMs());",
            "_runtime.SampleRenderInput();");

        string roomAfterStep = Slice(
            room,
            "    void OnAfterLogicStep(long _)",
            "    void EnsureRuntime()");
        AssertInOrder(
            roomAfterStep,
            "_runtime?.SendCommandAndPredict();");

        string pollAndApply = Slice(
            runtime,
            "    public void PollAndApply(long nowMs)",
            "    public void SampleRenderInput()");
        AssertInOrder(
            pollAndApply,
            "_session.Poll(nowMs);",
            "AcceptJoinIfReady();",
            "DrainApplicationMessages();",
            "EndIfSessionEnded();");

        string sendPredict = Slice(
            runtime,
            "    public void SendCommandAndPredict()",
            "    public void SampleSendPredict()");
        AssertInOrder(
            sendPredict,
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
            "_owner.ApplySnapshot(");

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
