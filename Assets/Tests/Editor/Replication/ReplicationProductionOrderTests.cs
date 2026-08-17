using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// W0 生产编排特征测试：冻结现有 Room/Simulation 方法内的关键调用顺序。
/// W2 删除旧 Room 入口时应由新 Session/Replication 编排测试替换本测试。
/// </summary>
public sealed class ReplicationProductionOrderTests
{
    /// <summary>Host 必须先收包写输入，再按 Combat/PostCombat/Commit/Capture/Send 顺序完成一格。</summary>
    [Test]
    public void HostFrame_ProductionSource_PreservesReceiveStepCaptureSendOrder()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomHost.cs");
        string simulation = ReadScript("App/Controllers/Gameplay/SimulationHost.cs");

        string roomUpdate = Slice(room, "    void Update()", "    void OnDisable()");
        AssertInOrder(
            roomUpdate,
            "_transport.Poll();",
            "DrainAuthorityInbox();",
            "TryAcceptPendingJoins();",
            "CheckGuestIdle();");

        string applyCommands = Slice(
            room,
            "    void ApplyGuestCommands(ClientCommand[] commands)",
            "    void CheckGuestIdle()");
        AssertInOrder(
            applyCommands,
            "CurrentFrame + 1",
            "RoomRemoteInputMerge.TryMergeUnapplied(",
            "buffer.Set(in merged);",
            "_guest.AppliedHintThisTick = newestHint;");

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
            "    void TryBindTransport()");
        AssertInOrder(
            afterStep,
            "CaptureAuthorityActors();",
            "CopyHits();",
            "new AuthorityTick(",
            "RoomCodec.WriteAuthorityTickEnvelope(",
            "_transport.Send(");
    }

    /// <summary>Client 必须先收权威并采样，再于逻辑步回调中先上行、后预测。</summary>
    [Test]
    public void ClientFrame_ProductionSource_PreservesReceiveSampleSendPredictOrder()
    {
        string client = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");

        string update = Slice(client, "    void Update()", "    void LateUpdate()");
        AssertInOrder(
            update,
            "_transport.Poll();",
            "DrainClientInbox();",
            "SampleRenderInput();",
            "_hostIdle.IsTimedOut(");

        string authorityTick = Slice(
            client,
            "    void OnAuthorityTick(byte[] body)",
            "    void ApplyRemoteActors(AuthorityTick tick)");
        AssertInOrder(
            authorityTick,
            "RoomCodec.ReadAuthorityTickEnvelope(",
            "ReplicationCodec.ReadAuthorityTick(",
            "ApplyRemoteActors(tick);",
            "TryFindSelf(tick, out ActorReplicationSnapshot self)",
            "_actionAck.Reconcile(",
            "_driver.Reconcile(",
            "PlayReplicatedHits(tick);");

        string afterStep = Slice(
            client,
            "    void OnAfterLogicStep(long _)",
            "    void SampleRenderInput()");
        AssertInOrder(
            afterStep,
            "_predictFrame++;",
            "_inputFrames.ResolveLocal(",
            "RememberCommand(in command);",
            "_transport.Send(",
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
