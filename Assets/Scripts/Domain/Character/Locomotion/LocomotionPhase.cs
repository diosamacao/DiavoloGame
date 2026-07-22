/// <summary>Locomotion 内层状态机 StateId；不升格为顶层 CharacterStateType。</summary>
public enum LocomotionPhase
{
    Idle = 0,
    Start = 1,
    Gait = 2,
    PivotTurn = 3,
    Stop = 4,
}
