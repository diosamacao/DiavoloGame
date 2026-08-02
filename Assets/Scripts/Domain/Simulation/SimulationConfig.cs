using System;

/// <summary>确定固定逻辑帧率、追帧预算与角色软弹开参数。</summary>
public sealed class SimulationConfig
{
    /// <summary>ACTGame 全局逻辑频率。</summary>
    public const int DefaultLogicHz = 60;

    /// <summary>单渲染帧默认最多补算的逻辑帧数。</summary>
    public const int DefaultMaxFrameCatchUp = 8;

    /// <summary>软弹开默认比例（千分比）；500 = 推开一半重叠。</summary>
    public const int DefaultSoftSeparationFactorMilli = 500;

    /// <summary>软弹开默认固定迭代次数。</summary>
    public const int DefaultSoftSeparationIterations = 3;

    /// <summary>每秒逻辑帧数。</summary>
    public int LogicHz { get; }

    /// <summary>单个逻辑帧的固定秒数。</summary>
    public float FixedDeltaSeconds { get; }

    /// <summary>单渲染帧最大追帧数；超额欠账保留到后续渲染帧。</summary>
    public int MaxFrameCatchUp { get; }

    /// <summary>是否在每逻辑帧末执行角色圆盘软弹开。</summary>
    public bool SoftBodySeparationEnabled { get; }

    /// <summary>软弹开强度（0～1000 千分比）。</summary>
    public int SoftSeparationFactorMilli { get; }

    /// <summary>软弹开固定迭代次数。</summary>
    public int SoftSeparationIterations { get; }

    /// <summary>创建固定帧配置；所有值在 World 创建后不可修改。</summary>
    public SimulationConfig(
        int logicHz = DefaultLogicHz,
        int maxFrameCatchUp = DefaultMaxFrameCatchUp,
        bool softBodySeparationEnabled = true,
        int softSeparationFactorMilli = DefaultSoftSeparationFactorMilli,
        int softSeparationIterations = DefaultSoftSeparationIterations)
    {
        if (logicHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(logicHz));
        if (maxFrameCatchUp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFrameCatchUp));
        if (softSeparationFactorMilli < 0 || softSeparationFactorMilli > SoftBodySeparation.FactorMilliMax)
            throw new ArgumentOutOfRangeException(nameof(softSeparationFactorMilli));
        if (softSeparationIterations < 0)
            throw new ArgumentOutOfRangeException(nameof(softSeparationIterations));

        LogicHz = logicHz;
        FixedDeltaSeconds = 1f / logicHz;
        MaxFrameCatchUp = maxFrameCatchUp;
        SoftBodySeparationEnabled = softBodySeparationEnabled;
        SoftSeparationFactorMilli = softSeparationFactorMilli;
        SoftSeparationIterations = softSeparationIterations;
    }
}
