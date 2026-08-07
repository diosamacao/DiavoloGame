/// <summary>玩法意图映射所需的角色上下文条件。</summary>
public enum GameplayIntentCondition
{
    /// <summary>不限制角色上下文。</summary>
    Always = 0,
    /// <summary>角色处于 Locomotion/Gait/Sprint。</summary>
    IsSprinting = 1,
    /// <summary>角色当前正在播放 Dodge 类型 Action。</summary>
    IsDodging = 2,
    /// <summary>Numeric Flags 武装了完美闪避反击缓冲。</summary>
    HasPerfectDodgeCounter = 3,
}
