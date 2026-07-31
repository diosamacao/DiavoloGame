using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证 SimulationWorld 的注册、稳定顺序与单帧推进语义。</summary>
public sealed class SimulationWorldTests
{
    /// <summary>每次 Step 必须只推进一帧并按注册 Id 顺序调用所有 Actor。</summary>
    [Test]
    public void Step_AdvancesOneFrameAndTicksActorsInIdOrder()
    {
        var trace = new List<string>();
        var world = new SimulationWorld(new SimulationConfig());
        world.Register(new RecordingActor("player", trace));
        world.Register(new RecordingActor("enemy", trace));

        world.Step();
        world.Step();

        Assert.That(world.CurrentFrame, Is.EqualTo(1));
        Assert.That(trace, Is.EqualTo(new[]
        {
            "0:player",
            "0:enemy",
            "1:player",
            "1:enemy"
        }));
    }

    /// <summary>World 分配的 Actor Id 必须单调增长且在注销后不复用。</summary>
    [Test]
    public void Register_AssignsMonotonicIdsWithoutReuse()
    {
        var world = new SimulationWorld(new SimulationConfig());
        SimActorRegistration first = world.Register(new RecordingActor("first", new List<string>()));
        SimActorRegistration second = world.Register(new RecordingActor("second", new List<string>()));

        Assert.That(world.Unregister(first), Is.True);
        SimActorRegistration third = world.Register(new RecordingActor("third", new List<string>()));

        Assert.That(first.Id.Value, Is.EqualTo(1));
        Assert.That(second.Id.Value, Is.EqualTo(2));
        Assert.That(third.Id.Value, Is.EqualTo(3));
    }

    /// <summary>注销后 Actor 不得继续参与后续逻辑帧。</summary>
    [Test]
    public void Unregister_RemovesActorFromFollowingSteps()
    {
        var trace = new List<string>();
        var world = new SimulationWorld(new SimulationConfig());
        SimActorRegistration registration = world.Register(new RecordingActor("actor", trace));

        Assert.That(world.Unregister(registration), Is.True);
        world.Step();

        Assert.That(trace, Is.Empty);
        Assert.That(world.ActorCount, Is.Zero);
    }

    /// <summary>渲染采样只调用实现可选接口的 Actor，且不会推进逻辑帧。</summary>
    [Test]
    public void SampleRenderFrame_DoesNotAdvanceSimulation()
    {
        var world = new SimulationWorld(new SimulationConfig());
        var actor = new RecordingRenderActor();
        world.Register(actor);

        world.SampleRenderFrame();

        Assert.That(actor.SampleCount, Is.EqualTo(1));
        Assert.That(world.CurrentFrame, Is.EqualTo(-1));
    }

    /// <summary>Render 必须只转发表现比例且不推进逻辑帧。</summary>
    [Test]
    public void Render_ForwardsInterpolationWithoutAdvancingSimulation()
    {
        var world = new SimulationWorld(new SimulationConfig());
        var actor = new RecordingRenderActor();
        world.Register(actor);

        world.Render(0.4f);

        Assert.That(actor.LastRenderAlpha, Is.EqualTo(0.4f));
        Assert.That(world.CurrentFrame, Is.EqualTo(-1));
    }

    /// <summary>测试用 Actor 记录 World 传入的逻辑帧与调用顺序。</summary>
    sealed class RecordingActor : ISimulationActor
    {
        readonly string _name;
        readonly List<string> _trace;

        /// <summary>创建带名称与共享调用轨迹的测试 Actor。</summary>
        public RecordingActor(string name, List<string> trace)
        {
            _name = name;
            _trace = trace;
        }

        /// <summary>记录当前固定逻辑帧，不执行其他副作用。</summary>
        public void Step(long frameIndex, float fixedDeltaSeconds)
        {
            _trace.Add($"{frameIndex}:{_name}");
        }
    }

    /// <summary>记录渲染采样次数的测试 Actor。</summary>
    sealed class RecordingRenderActor :
        ISimulationActor,
        IRenderFrameSampler,
        ISimulationRenderable
    {
        /// <summary>收到的渲染帧采样次数。</summary>
        public int SampleCount { get; private set; }

        /// <summary>最近收到的渲染插值比例。</summary>
        public float LastRenderAlpha { get; private set; }

        /// <summary>记录一次渲染输入采样。</summary>
        public void SampleRenderFrame() => SampleCount++;

        /// <summary>记录 World 转发的表现插值比例。</summary>
        public void Render(float interpolationAlpha) => LastRenderAlpha = interpolationAlpha;

        /// <summary>本用例不关心逻辑 Step。</summary>
        public void Step(long frameIndex, float fixedDeltaSeconds) { }
    }
}
