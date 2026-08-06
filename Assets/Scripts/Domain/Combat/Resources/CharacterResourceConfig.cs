using System;
using UnityEngine;

/// <summary>角色玩法资源上限与回复；嵌在 CharacterConfig，禁止 Profile 双轨。</summary>
[Serializable]
public sealed class CharacterResourceConfig
{
    [SerializeField] int maxEnergy = 120;
    [SerializeField] int energyRegenMilliPerFrame = 200;
    [SerializeField] int maxDecibel = 3000;
    [SerializeField] int maxDodgeCharges = 2;
    [SerializeField] int dodgeRechargeFrames = 60;
    [SerializeField] int combatHoldFrames = 180;

    /// <summary>默认数值（绝区零向骨架）。</summary>
    public static CharacterResourceConfig Default => new();

    public int MaxEnergy => Mathf.Max(1, maxEnergy);
    public int EnergyRegenMilliPerFrame => Mathf.Max(0, energyRegenMilliPerFrame);
    public int MaxDecibel => Mathf.Max(1, maxDecibel);
    public int MaxDodgeCharges => Mathf.Max(0, maxDodgeCharges);
    public int DodgeRechargeFrames => Mathf.Max(1, dodgeRechargeFrames);
    public int CombatHoldFrames => Mathf.Max(0, combatHoldFrames);
}
