/// <summary>参与帧末角色圆盘软弹开的 Actor；由 SimulationWorld 按 SimActorId 序收集。</summary>
public interface ISimSoftBodyParticipant
{
    /// <summary>当前水平电机；软弹开直接读写其毫米坐标。</summary>
    CharacterMotorSim MotorSim { get; }

    /// <summary>本帧是否参与互撞软弹开（死亡/特殊状态可关闭）。</summary>
    bool ParticipatesInSoftBodySeparation { get; }

    /// <summary>软弹开写回 MotorSim 后同步 Transform / 表现 Pose。</summary>
    void OnSoftBodySeparationApplied();
}
