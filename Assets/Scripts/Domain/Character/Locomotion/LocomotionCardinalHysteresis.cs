/// <summary>Gait 循环 Cardinal 最短驻留（L-DIR2）；纯函数便于 EditMode。</summary>
public static class LocomotionCardinalHysteresis
{
    /// <summary>
    /// 提案经最短驻留后采纳；None 视为 Forward。
    /// 返回应使用的 cardinal，并更新 current/dwellFrames。
    /// </summary>
    public static MoveCardinal Resolve(
        ref MoveCardinal current,
        ref int dwellFrames,
        MoveCardinal proposed,
        int minDwellFrames)
    {
        if (proposed == MoveCardinal.None)
            proposed = MoveCardinal.Forward;

        if (current == MoveCardinal.None)
        {
            current = proposed;
            dwellFrames = 0;
            return current;
        }

        if (proposed == current)
        {
            dwellFrames++;
            return current;
        }

        if (dwellFrames < minDwellFrames)
        {
            dwellFrames++;
            return current;
        }

        current = proposed;
        dwellFrames = 0;
        return current;
    }
}
