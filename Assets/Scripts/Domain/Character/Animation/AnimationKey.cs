/// <summary>Locomotion / 表现逻辑动画键；由 CharacterAnimationProfile 映射到 Clip。</summary>
public enum AnimationKey
{
    Idle = 0,
    Walk = 1,
    Run = 2,
    /// <summary>跑档起步（常绑 RunStart）。</summary>
    Start = 3,
    PivotTurn = 4,
    StopL = 5,
    StopR = 6,
    /// <summary>Run 持续达标后进入的冲刺循环。</summary>
    Sprint = 7,
    /// <summary>起步未完成松手时的收束（资产名 Run_Start_End）。</summary>
    StartEnd = 8,
    /// <summary>对峙/横移：本地左向走。</summary>
    WalkLeft = 9,
    /// <summary>对峙/横移：本地右向走。</summary>
    WalkRight = 10,
    /// <summary>走档正向起步；缺省时回退 Start。</summary>
    WalkStart = 11,
    /// <summary>走档左向起步；缺则 WalkStart → Start。</summary>
    WalkStartLeft = 12,
    /// <summary>走档右向起步；缺则 WalkStart → Start。</summary>
    WalkStartRight = 13,
    /// <summary>轻受击 Additive 默认键；不进 Locomotion 状态机。</summary>
    HitShake = 14,
}
