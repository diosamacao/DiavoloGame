using System;

/// <summary>
/// 把一批客机命令里尚未应用的 Hint 合并进同一权威帧。
/// 边沿 OR，轴与 Held 取最新，避免冗余包只留下最后一帧而丢掉 Attack。
/// </summary>
public static class RoomRemoteInputMerge
{
    /// <summary>
    /// 按 FrameHint 升序合并未应用命令到 targetFrame。
    /// 无新 Hint 时返回 false，不得清空已写入的下一帧输入。
    /// </summary>
    public static bool TryMergeUnapplied(
        ClientCommand[] commands,
        long lastAppliedHint,
        long targetFrame,
        SimActorId actorId,
        out InputFrame merged,
        out long newestHint)
    {
        merged = default;
        newestHint = lastAppliedHint;
        if (commands == null || commands.Length == 0 || !actorId.IsValid || targetFrame < 0)
            return false;

        int[] order = new int[commands.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (left, right) =>
            commands[left].FrameHint.CompareTo(commands[right].FrameHint));

        bool any = false;
        InputFrame accumulated = default;
        for (int i = 0; i < order.Length; i++)
        {
            ClientCommand command = commands[order[i]];
            if (command.FrameHint <= 0
                || !RoomRemoteInputPolicy.ShouldApply(command.FrameHint, newestHint))
                continue;

            InputFrame identified = command.Input.WithIdentity(targetFrame, actorId);
            if (!any)
            {
                accumulated = identified;
                any = true;
            }
            else
            {
                accumulated = accumulated.MergeSample(in identified);
            }

            newestHint = command.FrameHint;
        }

        if (!any)
            return false;

        merged = accumulated;
        return true;
    }
}
