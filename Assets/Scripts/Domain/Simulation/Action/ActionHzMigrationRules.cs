using System;

/// <summary>30Hz Action 数据迁移到 60Hz 时共用的确定性整数帧映射规则。</summary>
public static class ActionHzMigrationRules
{
    /// <summary>旧资产只有严格为 30Hz 时才允许迁移，防止重复执行造成帧号翻倍。</summary>
    public static bool ShouldMigrate(int sampleRate) => sampleRate == 30;

    /// <summary>把点事件或 AtFrame 帧映射为相同时刻的 60Hz 帧。</summary>
    public static int MapPointFrame(int frame) =>
        checked(Math.Max(0, frame) * 2);

    /// <summary>把 30Hz 闭区间映射为保持时长的 60Hz 闭区间。</summary>
    public static void MapClosedInterval(
        int startFrame,
        int endFrame,
        out int mappedStart,
        out int mappedEnd)
    {
        int start = Math.Max(0, startFrame);
        int end = Math.Max(start, endFrame);
        mappedStart = checked(start * 2);
        mappedEnd = checked(end * 2 + 1);
    }
}
