/// <summary>位移 Modifier/Command 的目标来源。</summary>
public enum MotionTargetSource
{
    /// <summary>动作起手固化的 ActionTargetId。</summary>
    ActionTarget = 0,

    /// <summary>当前战斗锁（吸附默认回退）。</summary>
    CurrentLock = 1,
}
