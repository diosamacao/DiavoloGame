using System;
using System.Collections.Generic;

/// <summary>按稳定 SimActorId 顺序推进所有已注册 Actor 的纯 C# 固定帧世界。</summary>
public sealed class SimulationWorld
{
    readonly SimulationConfig _config;
    readonly List<ActorEntry> _actors = new();
    readonly Dictionary<ISimulationActor, SimActorId> _idsByActor = new();
    readonly InputFrameBuffer _inputFrames = new();
    int _nextActorId = 1;
    bool _isStepping;

    /// <summary>最近完成的逻辑帧；尚未 Step 时为 -1。</summary>
    public long CurrentFrame { get; private set; } = -1;

    /// <summary>当前注册 Actor 数量。</summary>
    public int ActorCount => _actors.Count;

    /// <summary>固定逻辑帧秒数。</summary>
    public float FixedDeltaSeconds => _config.FixedDeltaSeconds;

    /// <summary>当前会话输入历史；供本地采样、AI、回放与未来权威包写入。</summary>
    public InputFrameBuffer InputFrames => _inputFrames;

    /// <summary>使用不可变配置创建空模拟世界。</summary>
    public SimulationWorld(SimulationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>分配单调稳定 Id 并注册 Actor；重复注册视为编程错误。</summary>
    public SimActorRegistration Register(ISimulationActor actor)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        EnsureNotStepping("注册");
        if (_idsByActor.ContainsKey(actor))
            throw new InvalidOperationException("同一个 Simulation Actor 不能重复注册。");

        var id = new SimActorId(_nextActorId++);
        _actors.Add(new ActorEntry(id, actor));
        _idsByActor.Add(actor, id);
        if (actor is ISimulationInputParticipant inputParticipant)
            inputParticipant.BindSimulationInput(id, _inputFrames);
        return new SimActorRegistration(id);
    }

    /// <summary>注销指定 Actor；无效或已注销句柄返回 false。</summary>
    public bool Unregister(SimActorRegistration registration)
    {
        if (!registration.IsValid)
            return false;
        EnsureNotStepping("注销");

        for (int i = 0; i < _actors.Count; i++)
        {
            ActorEntry entry = _actors[i];
            if (entry.Id != registration.Id)
                continue;

            _actors.RemoveAt(i);
            _idsByActor.Remove(entry.Actor);
            _inputFrames.RemoveActor(entry.Id);
            return true;
        }

        return false;
    }

    /// <summary>每渲染帧汇聚一次本地设备输入边沿，避免高 FPS 下无逻辑 Step 时漏输入。</summary>
    public void SampleRenderFrame()
    {
        EnsureNotStepping("采集渲染帧");
        long targetFrame = CurrentFrame + 1;
        for (int i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Actor is IRenderFrameSampler sampler)
                sampler.SampleRenderFrame(targetFrame);
        }
    }

    /// <summary>把前后逻辑 Pose 插值到渲染层；该过程不得改变权威模拟状态。</summary>
    public void Render(float interpolationAlpha)
    {
        EnsureNotStepping("渲染");
        for (int i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Actor is ISimulationRenderable renderable)
                renderable.Render(interpolationAlpha);
        }
    }

    /// <summary>严格推进一个逻辑帧，每个 Actor 按 SimActorId 升序执行一次。</summary>
    public void Step()
    {
        if (_isStepping)
            throw new InvalidOperationException("SimulationWorld 不允许递归 Step。");

        _isStepping = true;
        long frameIndex = CurrentFrame + 1;
        try
        {
            // 输入生产阶段先完成，保证所有 Actor 的玩法 Step 只读取同一逻辑帧输入。
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].Actor is ISimulationInputProducer producer)
                    producer.ProduceInput(frameIndex);
            }

            // Id 单调分配且列表只追加，因此遍历顺序天然稳定；注销不会重排剩余 Id。
            for (int i = 0; i < _actors.Count; i++)
            {
                ActorEntry entry = _actors[i];
                InputFrame input = _inputFrames.ResolveLocal(frameIndex, entry.Id);
                entry.Actor.Step(frameIndex, _config.FixedDeltaSeconds, in input);
            }

            CurrentFrame = frameIndex;
        }
        finally
        {
            _isStepping = false;
        }
    }

    /// <summary>禁止在 Actor Step 中直接改变 World 集合，后续 L0C 将统一改为帧末 Commit。</summary>
    void EnsureNotStepping(string operation)
    {
        if (_isStepping)
            throw new InvalidOperationException($"SimulationWorld Step 期间不能{operation} Actor。");
    }

    /// <summary>World 内部稳定 Actor 条目。</summary>
    readonly struct ActorEntry
    {
        public SimActorId Id { get; }
        public ISimulationActor Actor { get; }

        public ActorEntry(SimActorId id, ISimulationActor actor)
        {
            Id = id;
            Actor = actor;
        }
    }
}
