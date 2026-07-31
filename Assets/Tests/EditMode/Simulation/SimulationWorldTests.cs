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
        Assert.That(actor.LastSampleTargetFrame, Is.EqualTo(0));
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

    /// <summary>输入生产阶段必须先写当前帧，再由 Actor Step 消费同一 InputFrame。</summary>
    [Test]
    public void Step_ProducesInputBeforeActorConsumption()
    {
        var world = new SimulationWorld(new SimulationConfig());
        var actor = new RecordingInputActor();
        SimActorRegistration registration = world.Register(actor);

        world.Step();

        Assert.That(actor.ConsumedFrame.ActorId, Is.EqualTo(registration.Id));
        Assert.That(actor.ConsumedFrame.Frame, Is.Zero);
        Assert.That(actor.ConsumedFrame.WasPressed(InputButton.Attack), Is.True);
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
        public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame)
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

        /// <summary>最近一次渲染采样被分配的目标逻辑帧。</summary>
        public long LastSampleTargetFrame { get; private set; } = -1;

        /// <summary>记录一次渲染输入采样。</summary>
        public void SampleRenderFrame(long targetFrame)
        {
            SampleCount++;
            LastSampleTargetFrame = targetFrame;
        }

        /// <summary>记录 World 转发的表现插值比例。</summary>
        public void Render(float interpolationAlpha) => LastRenderAlpha = interpolationAlpha;

        /// <summary>本用例不关心逻辑 Step。</summary>
        public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame) { }
    }

    /// <summary>测试 World 的输入绑定、生产与消费阶段顺序。</summary>
    sealed class RecordingInputActor :
        ISimulationActor,
        ISimulationInputParticipant,
        ISimulationInputProducer
    {
        SimActorId _actorId;
        InputFrameBuffer _buffer;

        /// <summary>Actor Step 最近消费的输入。</summary>
        public InputFrame ConsumedFrame { get; private set; }

        /// <summary>保存 World 分配的身份与输入历史。</summary>
        public void BindSimulationInput(SimActorId actorId, InputFrameBuffer inputFrames)
        {
            _actorId = actorId;
            _buffer = inputFrames;
        }

        /// <summary>为当前逻辑帧写入 Attack 按下边沿。</summary>
        public void ProduceInput(long frameIndex)
        {
            var input = new InputFrame(
                frameIndex,
                _actorId,
                0,
                0,
                InputButtonMask.Of(InputButton.Attack),
                InputButtonMask.Of(InputButton.Attack),
                0ul);
            _buffer.Set(in input);
        }

        /// <summary>记录当前逻辑帧实际消费的输入。</summary>
        public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame)
        {
            ConsumedFrame = inputFrame;
        }
    }
}
