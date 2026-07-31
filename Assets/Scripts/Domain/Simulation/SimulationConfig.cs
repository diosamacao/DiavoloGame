using System;

/// <summary>确定固定逻辑帧率与单渲染帧最大追帧预算。</summary>
public sealed class SimulationConfig
{
    /// <summary>ACTGame 全局逻辑频率。</summary>
    public const int DefaultLogicHz = 60;

    /// <summary>单渲染帧默认最多补算的逻辑帧数。</summary>
    public const int DefaultMaxFrameCatchUp = 8;

    /// <summary>每秒逻辑帧数。</summary>
    public int LogicHz { get; }

    /// <summary>单个逻辑帧的固定秒数。</summary>
    public float FixedDeltaSeconds { get; }

    /// <summary>单渲染帧最大追帧数；超额欠账保留到后续渲染帧。</summary>
    public int MaxFrameCatchUp { get; }

    /// <summary>创建固定帧配置；所有值在 World 创建后不可修改。</summary>
    public SimulationConfig(
        int logicHz = DefaultLogicHz,
        int maxFrameCatchUp = DefaultMaxFrameCatchUp)
    {
        if (logicHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(logicHz));
        if (maxFrameCatchUp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFrameCatchUp));

        LogicHz = logicHz;
        FixedDeltaSeconds = 1f / logicHz;
        MaxFrameCatchUp = maxFrameCatchUp;
    }
}
