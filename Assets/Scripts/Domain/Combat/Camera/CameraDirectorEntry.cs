using System.Collections.Generic;

/// <summary>导演栈中的不可变模式条目。</summary>
public readonly struct CameraDirectorEntry
{
    /// <summary>创建模式条目。</summary>
    public CameraDirectorEntry(CameraMode mode, int priority)
    {
        Mode = mode;
        Priority = priority;
    }

    /// <summary>模式类型。</summary>
    public CameraMode Mode { get; }

    /// <summary>Cinemachine 抢权优先级。</summary>
    public int Priority { get; }
}
