using UnityEngine;

/// <summary>行为树节点作者时间（秒）与逻辑帧换算；运行时仍以整数帧推进。</summary>
public static class EnemyBehaviorTime
{
    /// <summary>逻辑帧率；与 SimulationHost 一致。</summary>
    public static int LogicHz => SimulationConfig.DefaultLogicHz;

    /// <summary>秒 → 逻辑帧；负值钳为 0，向上取整避免短冷却被抹成 0。</summary>
    public static int SecondsToFrames(float seconds)
    {
        if (seconds <= 0f)
            return 0;
        return Mathf.Max(1, Mathf.CeilToInt(seconds * LogicHz));
    }

    /// <summary>秒 → 等待类逻辑帧；至少 1 帧。</summary>
    public static int SecondsToWaitFrames(float seconds) =>
        Mathf.Max(1, SecondsToFrames(seconds));
}
