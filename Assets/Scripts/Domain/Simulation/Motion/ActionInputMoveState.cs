using System;

/// <summary>动作移动表现的线状态：低三位方向，其余位是本方向开始的动作帧。无需第二个时钟。</summary>
public static class ActionInputMoveState
{
    /// <summary>提取 Pose/前后左右方向槽。</summary>
    public static int Cardinal(int state) => state & 7;
    /// <summary>提取本方向开始的动作逻辑帧。</summary>
    public static int StartFrame(int state) => state >> 3;

    /// <summary>生成可复制的方向与相位状态；0..4 分别为静止/前/后/左/右。</summary>
    public static int Pack(int cardinal, int startFrame)
    {
        if (cardinal < 0 || cardinal > 4 || startFrame < 0 || startFrame > (int.MaxValue >> 3))
            throw new ArgumentOutOfRangeException();
        return (startFrame << 3) | cardinal;
    }

    /// <summary>拒绝非法方向或晚于动作时钟的相位，防止坏快照制造负时间。</summary>
    public static bool IsValid(int state, int actionFrame) =>
        state >= 0 && Cardinal(state) <= 4 && StartFrame(state) <= actionFrame;
}
