/// <summary>预测位移参数；速度用毫米，不引用 Unity CharacterMotorConfig。</summary>
public readonly struct PredictedLocomotionConfig
{
    /// <summary>默认走 4m/s、跑 7m/s、冲刺 9m/s、跑阈 0.6、转向 0.2s、纠偏 50mm。</summary>
    public static PredictedLocomotionConfig Default => new(
        walkSpeedMm: 4000,
        runSpeedMm: 7000,
        runThresholdMilli: 600,
        logicHz: SimulationConfig.DefaultLogicHz,
        reconcileThresholdMm: 50,
        rotationSmoothTimeSeconds: 0.2f,
        sprintSpeedMm: 9000);

    /// <summary>创建预测配置；非法值回退到 Default 对应项。</summary>
    public PredictedLocomotionConfig(
        int walkSpeedMm,
        int runSpeedMm,
        int runThresholdMilli,
        int logicHz,
        int reconcileThresholdMm,
        float rotationSmoothTimeSeconds = 0.2f,
        int sprintSpeedMm = 0)
    {
        WalkSpeedMm = walkSpeedMm > 0 ? walkSpeedMm : Default.WalkSpeedMm;
        RunSpeedMm = runSpeedMm > 0 ? runSpeedMm : Default.RunSpeedMm;
        RunThresholdMilli = runThresholdMilli < 0 ? 0 : (runThresholdMilli > 1000 ? 1000 : runThresholdMilli);
        LogicHz = logicHz > 0 ? logicHz : SimulationConfig.DefaultLogicHz;
        ReconcileThresholdMm = reconcileThresholdMm < 0 ? 0 : reconcileThresholdMm;
        RotationSmoothTimeSeconds = rotationSmoothTimeSeconds < 0f ? 0f : rotationSmoothTimeSeconds;
        SprintSpeedMm = sprintSpeedMm > 0 ? sprintSpeedMm : RunSpeedMm;
    }

    /// <summary>走速（毫米/秒）。</summary>
    public int WalkSpeedMm { get; }

    /// <summary>跑速（毫米/秒）。</summary>
    public int RunSpeedMm { get; }

    /// <summary>冲刺速度（毫米/秒）；由预览按权威 AnimationKey 选用。</summary>
    public int SprintSpeedMm { get; }

    /// <summary>输入幅度千分比达到该值用跑速（600 = 0.6）。</summary>
    public int RunThresholdMilli { get; }

    /// <summary>逻辑频率，用于把速度换成每帧位移。</summary>
    public int LogicHz { get; }

    /// <summary>水平误差超过该毫米则吸附权威并重放未确认输入。</summary>
    public int ReconcileThresholdMm { get; }

    /// <summary>FollowInput 朝向 SmoothDamp 时间（秒）；0 为瞬时锁 wish。</summary>
    public float RotationSmoothTimeSeconds { get; }
}
