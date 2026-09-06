/// <summary>进攻盒被弹刀后，攻击者是否进 Hit。默认 Interrupt 保持旧盒行为。</summary>
public enum ParriedActionPolicy
{
    /// <summary>进 HitState，播 Parried 选片，写 LightStun 边沿。</summary>
    Interrupt = 0,

    /// <summary>不停招、不写边沿、不 Reset BT；只卡当前进攻实例。</summary>
    Continue = 1,
}
