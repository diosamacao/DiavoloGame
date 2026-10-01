/// <summary>固定帧输入已经解析出的世界位移请求；纠偏重新执行碰撞，不重跑动作起手与时间轴。</summary>
public readonly struct ActionInputMovementCommand
{
    /// <summary>保存碰撞之前的位移请求及本帧朝向；零位移请求也可代表冻结帧。</summary>
    public ActionInputMovementCommand(SimVec2 delta, int facingMilliDeg, ActionBodyCollisionMode collisionMode, int skinMm)
    {
        IsPresent = true;
        Delta = delta;
        FacingMilliDeg = facingMilliDeg;
        CollisionMode = collisionMode;
        SkinMm = skinMm;
    }

    /// <summary>区别有效零位移与非输入移动帧。</summary>
    public bool IsPresent { get; }
    /// <summary>碰撞前的世界 XZ 请求（毫米）。</summary>
    public SimVec2 Delta { get; }
    /// <summary>本 Tick 完成后的朝向（毫度），由 Owner 记录口补齐。</summary>
    public int FacingMilliDeg { get; }
    /// <summary>重放必须使用当时的身体阻挡策略。</summary>
    public ActionBodyCollisionMode CollisionMode { get; }
    /// <summary>当时的身体接触间距（毫米）。</summary>
    public int SkinMm { get; }
}

/// <summary>只重演输入移动的电机请求；不得重启 Action、扣资源或派发 Notify。</summary>
public interface IActionInputMovementReplay
{
    /// <summary>从纠偏后的电机位置重新提交一条请求；不派发动作逻辑或表现事件。</summary>
    void ReplayMovement(in ActionInputMovementCommand command);
}
