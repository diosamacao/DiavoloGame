using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// App 生产编排特征测试：冻结 Session 收包、Gameplay 消费与 Simulation 步进顺序。
/// 纯 Session 状态转换由 ACTNet.Session.EditModeTests 覆盖。
/// </summary>
public sealed class ReplicationProductionOrderTests
{
    /// <summary>Host 必须先收包写输入，再按 Combat/PostCombat/Commit/Capture/Send 顺序完成一格。</summary>
    [Test]
    public void HostFrame_ProductionSource_PreservesReceiveStepCaptureSendOrder()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomHost.cs");
        string authority = ReadScript("App/Networking/Adapters/ActAuthorityReplicationAdapter.cs");
        string gameSession = ReadScript("App/Networking/Adapters/ActGameSessionHandler.cs");
        string simulation = ReadScript("App/Controllers/Gameplay/SimulationHost.cs");

        string roomUpdate = Slice(room, "    void Update()", "    void OnDisable()");
        AssertInOrder(
            roomUpdate,
            "_session.Poll(NowMs());",
            "DrainPlayerRequests();",
            "DrainApplicationMessages();");

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

        string simulationUpdate = Slice(
            simulation,
            "    void Update()",
            "    void LateUpdate()");
        AssertInOrder(
            simulationUpdate,
            "_combatHits.BeginFrame(",
            "_world.Step();",
            "_combatHits.ResolveBeforePostCombat(",
            "_world.ResolvePostCombat();",
            "_combatHits.CompleteFrame(",
            "CommitEnemyLifecycle();",
            "SendEvent(SimulationLogicStepEvent.Instance)",
            "AfterLogicStep?.Invoke(",
            "_frameHits.Clear();");

        string afterStep = Slice(
            room,
            "    void OnAfterLogicStep(long authorityFrame)",
            "    void DrainPlayerRequests()");
        AssertInOrder(
            afterStep,
            "CaptureAuthorityActors();",
            "CopyHits();",
            "ActReplicationApplicationPayloadCodec.Encode(",
            "_replicationServer.BuildFrame(",
            "ReplicationFrameCodec.Encode(frame);",
            "_session.SendApplication(");

        string spawnGuest = Slice(
            room,
            "    bool TrySpawnGuest(",
            "    void ApplyGuestCommands(ClientCommand[] commands)");
        AssertInOrder(
            spawnGuest,
            "_gameSession.TryCreateGuest(",
            "_replicationServer = new ReplicationServer();",
            "_guest = guest;",
            "_session.AcceptPlayer(");
        Assert.That(room, Does.Not.Contain("CharacterActorFactory.Create("));
        Assert.That(room, Does.Not.Contain("new GameObject(\"RemotePlayer\")"));

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
    }

    /// <summary>Client 必须先收权威并采样，再于逻辑步回调中先上行、后预测。</summary>
    [Test]
    public void ClientFrame_ProductionSource_PreservesReceiveSampleSendPredictOrder()
    {
        string client = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");

        string update = Slice(client, "    void Update()", "    void LateUpdate()");
        AssertInOrder(
            update,
            "_session.Poll(NowMs());",
            "SyncSessionState();",
            "DrainApplicationMessages();",
            "if (_joined && !_ended)",
            "SampleRenderInput();");

        string replicationFrame = Slice(
            client,
            "    void OnReplicationFrame(byte[] body)",
            "    void ApplySpawns(");
        AssertInOrder(
            replicationFrame,
            "ReplicationFrameCodec.Decode(body);",
            "_replicationClient.ApplyFrame(frame);",
            "ActReplicationApplicationPayloadCodec.Decode(",
            "ApplySpawns(",
            "ApplyUpdates(",
            "ApplyDespawns(",
            "ApplyOwnerSnapshot(",
            "PlayReplicatedHits(application.Hits);");

        string afterStep = Slice(
            client,
            "    void OnAfterLogicStep(long _)",
            "    void SampleRenderInput()");
        AssertInOrder(
            afterStep,
            "_predictFrame++;",
            "_inputFrames.ResolveLocal(",
            "RememberCommand(in command);",
            "_session.SendApplication(",
            "actor.Step(",
            "actor.ResolvePostCombat(",
            "_driver.RecordAutonomous(in input);");
    }

    /// <summary>从 Assets 相对路径读取当前生产脚本，确保测试锁定真实调用点。</summary>
    static string ReadScript(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, "Scripts", relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }

    /// <summary>截取两个唯一方法签名之间的源码，避免同名调用干扰顺序断言。</summary>
    static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"缺少起始标记：{startMarker}");
        Assert.That(end, Is.GreaterThan(start), $"缺少结束标记：{endMarker}");
        return source.Substring(start, end - start);
    }

    /// <summary>要求所有关键调用标记按给定次序出现，任一缺失或逆序都失败。</summary>
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
