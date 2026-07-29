/// <summary>进入动作图节点时由上层执行的上下文行为。</summary>
public enum ActionGraphStartBehaviorType
{
    /// <summary>按已缓冲移动意图修正动作起手朝向。</summary>
    FaceBufferedMoveIntent = 0,

    /// <summary>按节点配置切换战斗模式。</summary>
    SwitchCombatMode = 1,
}
