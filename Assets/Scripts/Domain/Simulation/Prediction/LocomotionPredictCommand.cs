/// <summary>走跑预测命令：输入加 ACT 重放跳过标志。Coordinator 不解读这些标志。</summary>
public readonly struct LocomotionPredictCommand
{
    /// <summary>创建一条位移预测命令。</summary>
    public LocomotionPredictCommand(in InputFrame input, bool skipWishReplay, bool skipRunnerReplay)
    {
        Input = input;
        SkipWishReplay = skipWishReplay;
        SkipRunnerReplay = skipRunnerReplay;
    }

    /// <summary>该 Tick 的量化输入。</summary>
    public InputFrame Input { get; }

    /// <summary>无 Runner 时禁止 ApplyInput 重放（Autonomous 记账）。</summary>
    public bool SkipWishReplay { get; }

    /// <summary>有 Runner 时禁止 ReplayTick（贴齐/烘焙帧）。</summary>
    public bool SkipRunnerReplay { get; }
}
