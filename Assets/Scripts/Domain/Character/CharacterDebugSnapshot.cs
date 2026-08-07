using System;

/// <summary>角色只读调试快照；由 CharacterActor.BuildDebugSnapshot 填充，HUD 禁止写回。</summary>
public readonly struct CharacterDebugSnapshot
{
    /// <summary>组装一帧调试快照（含 Numeric Attribute / Effects / Flags）。</summary>
    public CharacterDebugSnapshot(
        CharacterStateType state,
        bool actionActive,
        string actionName,
        int actionFrame,
        int actionTotalFrames,
        int freezeFrames,
        float currentHp,
        float maxHp,
        int energyPoints,
        int maxEnergy,
        int energyRegenMilliPerFrame,
        int decibel,
        int maxDecibel,
        int dodgeCharges,
        int maxDodgeCharges,
        int dodgeRechargeFramesLeft,
        bool inCombat,
        int inCombatHoldFrames,
        int perfectDodgeCounterFrames,
        int attackPoints,
        int defensePoints,
        int outgoingDamageMultMilli,
        int incomingDamageMultMilli,
        NumericEffectDebugEntry[] activeEffects,
        string nextSpecialForm,
        bool hasLock,
        string lockTargetName,
        float lockDistanceMeters,
        int motorXMm,
        int motorZMm,
        int motorYMm,
        int motorFacingMilliDeg,
        int softBodyMass,
        bool softBodyImmovable,
        int actionLateralPeakMm,
        GameplayIntentType[] frameIntents,
        BufferedIntentDebug[] buffers)
    {
        State = state;
        ActionActive = actionActive;
        ActionName = actionName ?? string.Empty;
        ActionFrame = actionFrame;
        ActionTotalFrames = actionTotalFrames;
        FreezeFrames = freezeFrames;
        CurrentHp = currentHp;
        MaxHp = maxHp;
        EnergyPoints = energyPoints;
        MaxEnergy = maxEnergy;
        EnergyRegenMilliPerFrame = energyRegenMilliPerFrame;
        Decibel = decibel;
        MaxDecibel = maxDecibel;
        DodgeCharges = dodgeCharges;
        MaxDodgeCharges = maxDodgeCharges;
        DodgeRechargeFramesLeft = dodgeRechargeFramesLeft;
        InCombat = inCombat;
        InCombatHoldFrames = inCombatHoldFrames;
        PerfectDodgeCounterFrames = perfectDodgeCounterFrames;
        AttackPoints = attackPoints;
        DefensePoints = defensePoints;
        OutgoingDamageMultMilli = outgoingDamageMultMilli;
        IncomingDamageMultMilli = incomingDamageMultMilli;
        ActiveEffects = activeEffects ?? Array.Empty<NumericEffectDebugEntry>();
        NextSpecialForm = nextSpecialForm ?? "-";
        HasLock = hasLock;
        LockTargetName = lockTargetName ?? string.Empty;
        LockDistanceMeters = lockDistanceMeters;
        MotorXMm = motorXMm;
        MotorZMm = motorZMm;
        MotorYMm = motorYMm;
        MotorFacingMilliDeg = motorFacingMilliDeg;
        SoftBodyMass = softBodyMass;
        SoftBodyImmovable = softBodyImmovable;
        ActionLateralPeakMm = actionLateralPeakMm;
        FrameIntents = frameIntents ?? Array.Empty<GameplayIntentType>();
        Buffers = buffers ?? Array.Empty<BufferedIntentDebug>();
    }

    public CharacterStateType State { get; }
    public bool ActionActive { get; }
    public string ActionName { get; }
    public int ActionFrame { get; }
    public int ActionTotalFrames { get; }
    public int FreezeFrames { get; }
    public float CurrentHp { get; }
    public float MaxHp { get; }
    public int EnergyPoints { get; }
    public int MaxEnergy { get; }
    public int EnergyRegenMilliPerFrame { get; }
    public int Decibel { get; }
    public int MaxDecibel { get; }
    public int DodgeCharges { get; }
    public int MaxDodgeCharges { get; }
    public int DodgeRechargeFramesLeft { get; }
    public bool InCombat { get; }
    public int InCombatHoldFrames { get; }
    /// <summary>完美反击缓冲剩余逻辑帧（Flags）。</summary>
    public int PerfectDodgeCounterFrames { get; }
    public int AttackPoints { get; }
    public int DefensePoints { get; }
    public int OutgoingDamageMultMilli { get; }
    public int IncomingDamageMultMilli { get; }
    /// <summary>当前 ActiveEffect 列表（只读拷贝）。</summary>
    public NumericEffectDebugEntry[] ActiveEffects { get; }
    /// <summary>下一发 Special 同键形态：EX / Special / -。</summary>
    public string NextSpecialForm { get; }
    public bool HasLock { get; }
    public string LockTargetName { get; }
    public float LockDistanceMeters { get; }
    public int MotorXMm { get; }
    public int MotorZMm { get; }
    public int MotorYMm { get; }
    public int MotorFacingMilliDeg { get; }
    public int SoftBodyMass { get; }
    public bool SoftBodyImmovable { get; }
    public int ActionLateralPeakMm { get; }
    public GameplayIntentType[] FrameIntents { get; }
    public BufferedIntentDebug[] Buffers { get; }
}
