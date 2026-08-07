using System;
using UnityEngine;

/// <summary>
/// 角色数值 Base 初始化配置（milli 由系统按整点 ×1000 写入）。
/// G3：运行时由 ResourceConfig + MaxHealth 合成；不再以 ResourceSim 为权威。
/// </summary>
[Serializable]
public sealed class CharacterNumericConfig
{
    [SerializeField] int maxHealth = 100;
    [SerializeField] int maxEnergy = 120;
    [SerializeField] int energyRegenMilliPerFrame = 200;
    [SerializeField] int maxDecibel = 3000;
    [SerializeField] int maxDodgeCharges = 2;
    [SerializeField] int dodgeRechargeFrames = 60;
    [SerializeField] int combatHoldFrames = 180;
    [SerializeField] int perfectDodgeCounterFrames = 45;
    [SerializeField] int attack = 10;
    [SerializeField] int defense = 0;

    /// <summary>与 Wave 3 资源默认对齐的数值配置。</summary>
    public static CharacterNumericConfig Default => new();

    public int MaxHealthPoints => Mathf.Max(1, maxHealth);
    public int MaxEnergyPoints => Mathf.Max(1, maxEnergy);
    public int EnergyRegenMilliPerFrame => Mathf.Max(0, energyRegenMilliPerFrame);
    public int MaxDecibelPoints => Mathf.Max(1, maxDecibel);
    public int MaxDodgeChargesPoints => Mathf.Max(0, maxDodgeCharges);
    public int DodgeRechargeFrames => Mathf.Max(1, dodgeRechargeFrames);
    public int CombatHoldFrames => Mathf.Max(0, combatHoldFrames);
    public int PerfectDodgeCounterFrames => Mathf.Max(0, perfectDodgeCounterFrames);
    public int AttackPoints => Mathf.Max(0, attack);
    public int DefensePoints => Mathf.Max(0, defense);

    /// <summary>整点 → Attribute milli。</summary>
    public static int ToMilli(int points) => points * 1000;

    /// <summary>
    /// 从现有 CharacterResourceConfig + 战斗 MaxHealth 合成 Numeric 配置（不改资产结构）。
    /// </summary>
    public static CharacterNumericConfig FromResourceConfig(
        CharacterResourceConfig resources,
        float maxHealthPoints,
        int attackPoints = 10,
        int defensePoints = 0)
    {
        CharacterResourceConfig src = resources ?? CharacterResourceConfig.Default;
        return new CharacterNumericConfig
        {
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealthPoints)),
            maxEnergy = src.MaxEnergy,
            energyRegenMilliPerFrame = src.EnergyRegenMilliPerFrame,
            maxDecibel = src.MaxDecibel,
            maxDodgeCharges = src.MaxDodgeCharges,
            dodgeRechargeFrames = src.DodgeRechargeFrames,
            combatHoldFrames = src.CombatHoldFrames,
            perfectDodgeCounterFrames = 45,
            attack = Mathf.Max(0, attackPoints),
            defense = Mathf.Max(0, defensePoints),
        };
    }
}
