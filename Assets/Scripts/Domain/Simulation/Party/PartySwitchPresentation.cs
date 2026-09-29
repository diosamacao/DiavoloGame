
/// <summary>换人时新旧角色是否允许短暂同时留场。</summary>
public enum PartySwitchPresentation
{
    /// <summary>旧角色收招期间，新角色同时播放 SwitchIn。</summary>
    DualPresence = 0,

    /// <summary>旧角色当帧退场，新角色直接播放支援动作。</summary>
    InstantReplace = 1,
}
