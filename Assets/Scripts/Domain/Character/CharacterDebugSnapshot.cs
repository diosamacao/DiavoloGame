using System;

/// <summary>角色只读调试快照；由 CharacterActor.BuildDebugSnapshot 填充，HUD 禁止写回。</summary>
public readonly struct CharacterDebugSnapshot
{
    /// <summary>组装一帧调试快照。</summary>
    public CharacterDebugSnapshot(
        CharacterStateType state,
        bool actionActive,
        string actionName,
        int actionFrame,
        int actionTotalFrames,
        int freezeFrames,
        float currentHp,
        float maxHp,
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
    public bool HasLock { get; }
    public string LockTargetName { get; }
    public float LockDistanceMeters { get; }
    public int MotorXMm { get; }
    public int MotorZMm { get; }
    public int MotorYMm { get; }
    public int MotorFacingMilliDeg { get; }
    public int SoftBodyMass { get; }
    public bool SoftBodyImmovable { get; }

    /// <summary>当前招式会话内 Motor 局部横向位移峰峰值（毫米，绝对值最大）。</summary>
    public int ActionLateralPeakMm { get; }

    public GameplayIntentType[] FrameIntents { get; }
    public BufferedIntentDebug[] Buffers { get; }
}
