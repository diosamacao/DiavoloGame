/// <summary>GAS-lite 属性标识；Current/Base 均为 milli-int（显示通常 /1000）。</summary>
public enum AttributeId : byte
{
    Health = 0,
    MaxHealth = 1,
    Energy = 2,
    MaxEnergy = 3,
    /// <summary>每逻辑帧回复的 Energy milli（值本身已是 energy-milli，不再二次 ×1000）。</summary>
    EnergyRegenMilliPerFrame = 4,
    Decibel = 5,
    MaxDecibel = 6,
    DodgeCharges = 7,
    MaxDodgeCharges = 8,
    /// <summary>单次闪避充能所需逻辑帧；存储为 frames×1000，Points=/1000。</summary>
    DodgeRechargeFrames = 9,
    Attack = 10,
    Defense = 11,
    /// <summary>出伤倍率；Base=1000 表示 ×1.0。</summary>
    OutgoingDamageMult = 12,
    /// <summary>承伤倍率；Base=1000 表示 ×1.0。</summary>
    IncomingDamageMult = 13,
}
