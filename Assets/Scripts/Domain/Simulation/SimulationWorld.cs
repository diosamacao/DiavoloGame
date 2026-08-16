using System;
using System.Collections.Generic;

/// <summary>按稳定 SimActorId 顺序推进所有已注册 Actor 的纯 C# 固定帧世界。</summary>
public sealed class SimulationWorld
{
    readonly SimulationConfig _config;
    readonly List<ActorEntry> _actors = new();
    readonly Dictionary<ISimulationActor, SimActorId> _idsByActor = new();
    readonly InputFrameBuffer _inputFrames = new();
    readonly List<ISimSoftBodyParticipant> _softBodyParticipants = new();
    SimVec2[] _softBodyPositions = Array.Empty<SimVec2>();
    int[] _softBodyRadiiMm = Array.Empty<int>();
    int[] _softBodyMasses = Array.Empty<int>();
    int _nextActorId = 1;
    bool _isStepping;
    long _lastPostCombatFrame = -1;

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
        // 边沿写入「下一逻辑帧」槽，本帧尚未 Step
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

            // 全体 wish/静态位移完成后做角色软弹开，保证互撞不依赖 Actor Step 顺序。
            ResolveSoftBodySeparation();

            CurrentFrame = frameIndex;
        }
        finally
        {
            _isStepping = false;
        }
    }

    /// <summary>按 SimActorId 序收集圆盘，软弹开后写回并同步表现。</summary>
    void ResolveSoftBodySeparation()
    {
        if (!_config.SoftBodySeparationEnabled
            || _config.SoftSeparationIterations <= 0
            || _config.SoftSeparationFactorMilli <= 0)
            return;

        _softBodyParticipants.Clear();
        for (int i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Actor is ISimSoftBodyParticipant soft
                && soft.ParticipatesInSoftBodySeparation
                && soft.MotorSim != null)
                _softBodyParticipants.Add(soft);
        }

        int count = _softBodyParticipants.Count;
        if (count < 2)
            return;

        EnsureSoftBodyBuffers(count);
        for (int i = 0; i < count; i++)
        {
            CharacterMotorSim motor = _softBodyParticipants[i].MotorSim;
            _softBodyPositions[i] = motor.PositionMm;
            _softBodyRadiiMm[i] = motor.RadiusMm;
            _softBodyMasses[i] = motor.SoftBodyMass;
        }

        SoftBodySeparation.Resolve(
            _softBodyPositions,
            _softBodyRadiiMm,
            _softBodyMasses,
            count,
            _config.SoftSeparationFactorMilli,
            _config.SoftSeparationIterations);

        for (int i = 0; i < count; i++)
        {
            SimVec2 pos = _softBodyPositions[i];
            _softBodyParticipants[i].MotorSim.CommitSoftSeparatedPosition(pos.X, pos.Z);
            _softBodyParticipants[i].OnSoftBodySeparationApplied();
        }
    }

    void EnsureSoftBodyBuffers(int count)
    {
        if (_softBodyPositions.Length < count)
            _softBodyPositions = new SimVec2[count];
        if (_softBodyRadiiMm.Length < count)
            _softBodyRadiiMm = new int[count];
        if (_softBodyMasses.Length < count)
            _softBodyMasses = new int[count];
    }

    /// <summary>在统一命中结算后按稳定 Actor Id 执行依赖命中结果的同帧动作收尾。</summary>
    public void ResolvePostCombat()
    {
        EnsureNotStepping("执行 PostCombat");
        // 尚未 Step 或本帧已收尾：禁止重复跑 OnHitConfirm
        if (CurrentFrame < 0 || _lastPostCombatFrame == CurrentFrame)
            return;

        for (int i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Actor is ISimulationPostCombatActor postCombatActor)
                postCombatActor.ResolvePostCombat(CurrentFrame);
        }

        _lastPostCombatFrame = CurrentFrame;
    }

    /// <summary>禁止在 Actor Step 中直接改变 World 集合；生命周期统一由帧末 Commit 入口处理。</summary>
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
