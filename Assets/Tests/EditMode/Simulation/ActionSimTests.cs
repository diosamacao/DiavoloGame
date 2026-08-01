using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证 ActionSim 的 60Hz 帧边界、延迟切招、命中衔接、自然结束与硬打断。</summary>
public sealed class ActionSimTests
{
    /// <summary>每个 World Step 必须恰好推进一帧，TotalFrames 哨兵也必须派发给外层时间轴。</summary>
    [Test]
    public void Step_AdvancesOneFrameAtSixtyHzAndTerminates()
    {
        var content = new FakeContent(totalFrames: 2);
        var sim = new ActionSim();
        var events = new List<ActionSimEvent>();

        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(content)), Is.True);
        Assert.That(sim.CurrentFrame, Is.Zero);
        sim.Step();
        Assert.That(sim.CurrentFrame, Is.EqualTo(1));
        sim.Step();
        Assert.That(sim.CurrentFrame, Is.EqualTo(2));
        Assert.That(sim.IsComplete, Is.True);

        sim.DrainEvents(events);
        Assert.That(events.Exists(item =>
            item.Frame == 2 && item.Type == ActionSimEventType.FrameAdvanced), Is.True);

        sim.ResolvePostCombat();
        Assert.That(sim.IsActive, Is.False);
        Assert.That(sim.HasEndedActionInstance(1), Is.True);
    }

    /// <summary>Cancel 在判定帧只排队，目标动作必须到下一 World 帧才以 frame 0 提交。</summary>
    [Test]
    public void Step_CancelQueuesUntilNextWorldFrame()
    {
        var first = new FakeContent(totalFrames: 5, cancelFrame: 1);
        var second = new FakeContent(totalFrames: 4);
        var graph = new FakeGraph();
        graph.CancelIntents.Add(GameplayIntentType.Attack);
        var resolver = new FakeResolver
        {
            CancelResult = ActionSimResolveResult.FromGraph(
                second,
                graph,
                "second",
                GameplayIntentType.Attack),
        };
        var buffer = new FakeBuffer(GameplayIntentType.Attack);
        var sim = new ActionSim(resolver, buffer);

        Assert.That(sim.TryStart(ActionSimResolveResult.FromGraph(first, graph, "first")), Is.True);
        int firstInstanceId = sim.InstanceId;
        sim.Step();

        Assert.That(sim.Snapshot.Content, Is.SameAs(first));
        Assert.That(sim.CurrentFrame, Is.EqualTo(1));
        Assert.That(buffer.HasBuffer(GameplayIntentType.Attack), Is.False);

        sim.Step();
        Assert.That(sim.Snapshot.Content, Is.SameAs(second));
        Assert.That(sim.CurrentFrame, Is.Zero);
        Assert.That(sim.InstanceId, Is.GreaterThan(firstInstanceId));
    }

    /// <summary>同帧命中确认应在 PostCombat 排队 OnHit 转换，并于下一 World 帧提交。</summary>
    [Test]
    public void ResolvePostCombat_HitConfirmQueuesTransitionForNextWorldFrame()
    {
        var first = new FakeContent(totalFrames: 5);
        var second = new FakeContent(totalFrames: 3);
        var graph = new FakeGraph
        {
            RequireConfirmedHit = true,
            AutomaticResult = ActionSimResolveResult.FromContent(second),
        };
        var sim = new ActionSim();

        Assert.That(sim.TryStart(ActionSimResolveResult.FromGraph(first, graph, "first")), Is.True);
        int hitInstanceId = sim.InstanceId;
        Assert.That(sim.ConfirmHit(hitInstanceId), Is.True);
        Assert.That(sim.ConfirmHit(hitInstanceId + 1), Is.False);

        sim.ResolvePostCombat();
        Assert.That(sim.Snapshot.Content, Is.SameAs(first));

        sim.Step();
        Assert.That(sim.Snapshot.Content, Is.SameAs(second));
        Assert.That(sim.CurrentFrame, Is.Zero);
    }

    /// <summary>无自动衔接的动作到达终止哨兵后应在 PostCombat 立即自然停止。</summary>
    [Test]
    public void ResolvePostCombat_CompleteActionStopsNaturallyOnNextStep()
    {
        var content = new FakeContent(totalFrames: 1);
        var sim = new ActionSim();

        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(content)), Is.True);
        sim.Step();
        Assert.That(sim.IsActive, Is.True);
        Assert.That(sim.IsComplete, Is.True);

        sim.ResolvePostCombat();
        Assert.That(sim.IsActive, Is.False);
    }

    /// <summary>硬打断仅接受严格更高优先级，并在当前帧立即建立新动作实例。</summary>
    [Test]
    public void TryInterrupt_RequiresStrictlyHigherPriorityAndInterruptsImmediately()
    {
        var current = new FakeContent(totalFrames: 5, interruptPriority: 10);
        var equal = new FakeContent(totalFrames: 5, interruptPriority: 10);
        var higher = new FakeContent(totalFrames: 5, interruptPriority: 11);
        var sim = new ActionSim();

        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(current)), Is.True);
        int firstInstanceId = sim.InstanceId;
        Assert.That(sim.TryInterrupt(ActionSimResolveResult.FromContent(equal)), Is.False);
        Assert.That(sim.Snapshot.Content, Is.SameAs(current));

        Assert.That(sim.TryInterrupt(ActionSimResolveResult.FromContent(higher)), Is.True);
        Assert.That(sim.Snapshot.Content, Is.SameAs(higher));
        Assert.That(sim.CurrentFrame, Is.Zero);
        Assert.That(sim.InstanceId, Is.GreaterThan(firstInstanceId));
    }

    /// <summary>提供测试所需的 60Hz 动作内容和可配置窗口。</summary>
    sealed class FakeContent : IActionSimContent
    {
        readonly int _cancelFrame;

        /// <summary>创建可指定长度、取消帧和打断优先级的测试内容。</summary>
        public FakeContent(int totalFrames, int cancelFrame = -1, int interruptPriority = 0)
        {
            TotalFrames = totalFrames;
            _cancelFrame = cancelFrame;
            InterruptPriority = interruptPriority;
        }

        /// <summary>测试内容始终已完成模拟迁移。</summary>
        public bool IsSimulationReady => true;

        /// <summary>测试内容固定使用 60Hz。</summary>
        public int SampleRate => ActionSim.LogicHz;

        /// <summary>测试动作的终止哨兵帧。</summary>
        public int TotalFrames { get; }

        /// <summary>测试动作的硬打断优先级。</summary>
        public int InterruptPriority { get; }

        /// <summary>测试内容允许在全部有效帧被硬打断。</summary>
        public bool IsInterruptibleAtFrame(int frame) => frame < TotalFrames;

        /// <summary>仅在配置帧开放 Normal 取消窗口。</summary>
        public bool IsCancelWindowActiveAtFrame(CancelWindowType windowType, int frame) =>
            windowType == CancelWindowType.Normal && frame == _cancelFrame;

        /// <summary>测试内容不开放 Recovery 入口重开。</summary>
        public bool AllowsRecoveryEntryRestartAtFrame(int frame) => false;

        /// <summary>测试内容不开放移动取消。</summary>
        public bool AllowsMovementCancelAtFrame(int frame) => false;

    }

    /// <summary>提供取消候选与可选命中自动衔接的测试动作图。</summary>
    sealed class FakeGraph : IActionSimGraph
    {
        /// <summary>图取消路由可接受的意图集合。</summary>
        public HashSet<GameplayIntentType> CancelIntents { get; } =
            new HashSet<GameplayIntentType>();

        /// <summary>自动衔接是否要求当前动作先确认命中。</summary>
        public bool RequireConfirmedHit { get; set; }

        /// <summary>满足条件时返回的自动衔接结果。</summary>
        public ActionSimResolveResult AutomaticResult { get; set; }

        /// <summary>把测试图配置的取消意图写入结果集合。</summary>
        public void CollectCancelCandidateIntents(
            string nodeId,
            CancelWindowType windowType,
            ISet<GameplayIntentType> results)
        {
            foreach (GameplayIntentType intent in CancelIntents)
                results.Add(intent);
        }

        /// <summary>满足命中条件且配置了结果时返回自动衔接。</summary>
        public bool TryResolveAutomaticTransition(
            string nodeId,
            IActionSimContent content,
            int currentFrame,
            bool hasConfirmedHit,
            out ActionSimResolveResult result,
            out bool shouldStop)
        {
            shouldStop = false;
            result = AutomaticResult;
            return result.IsValid && (!RequireConfirmedHit || hasConfirmedHit);
        }
    }

    /// <summary>为取消与 Recovery 测试返回预设解析结果。</summary>
    sealed class FakeResolver : IActionSimResolver
    {
        /// <summary>Cancel 查询返回的预设结果。</summary>
        public ActionSimResolveResult CancelResult { get; set; }

        /// <summary>Recovery 查询返回的预设结果。</summary>
        public ActionSimResolveResult RecoveryResult { get; set; }

        /// <summary>测试仅暴露 Attack 意图。</summary>
        public IEnumerable<GameplayIntentType> EnumerateActiveIntents()
        {
            yield return GameplayIntentType.Attack;
        }

        /// <summary>意图为 Attack 时返回预设 Cancel 结果。</summary>
        public bool TryResolveNext(
            GameplayIntentType intent,
            CancelWindowType windowType,
            in ActionSimSnapshot snapshot,
            out ActionSimResolveResult result)
        {
            result = CancelResult;
            return intent == GameplayIntentType.Attack && result.IsValid;
        }

        /// <summary>意图为 Attack 时返回预设 Recovery 结果。</summary>
        public bool TryResolveRecoveryStart(
            GameplayIntentType intent,
            in ActionSimSnapshot snapshot,
            out ActionSimResolveResult result)
        {
            result = RecoveryResult;
            return intent == GameplayIntentType.Attack && result.IsValid;
        }
    }

    /// <summary>以集合实现可观测消费行为的测试输入缓冲。</summary>
    sealed class FakeBuffer : IActionInputBuffer
    {
        readonly HashSet<GameplayIntentType> _buffered;

        /// <summary>以给定意图初始化测试缓冲。</summary>
        public FakeBuffer(params GameplayIntentType[] intents)
        {
            _buffered = new HashSet<GameplayIntentType>(intents);
        }

        /// <summary>返回指定意图是否仍在测试缓冲中。</summary>
        public bool HasBuffer(GameplayIntentType intent) => _buffered.Contains(intent);

        /// <summary>消费并移除指定测试意图。</summary>
        public bool TryConsumeBuffer(GameplayIntentType intent) => _buffered.Remove(intent);
    }
}
