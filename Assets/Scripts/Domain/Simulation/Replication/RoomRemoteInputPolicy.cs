/// <summary>
/// 远端命令是否写入下一权威帧。FrameHint 是客机序号，不是 Host 逻辑帧。
/// </summary>
public static class RoomRemoteInputPolicy
{
    /// <summary>
    /// 只接受比已应用更新的 Hint。同等 Hint 是冗余重发，再写一次会把已结算的 Attack 边沿打到下一帧。
    /// 禁止用 Host.CurrentFrame 与 FrameHint 比较。
    /// </summary>
    public static bool ShouldApply(long frameHint, long lastAppliedHint)
    {
        if (frameHint <= 0)
            return false;
        if (lastAppliedHint <= 0)
            return true;
        return frameHint > lastAppliedHint;
    }
}
