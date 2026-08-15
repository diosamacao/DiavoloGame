/// <summary>
/// 角色装配座位。差异只在 <see cref="CharacterActorFactory"/> 能力图，禁止在 Step 里用 isClient 开双轨。
/// </summary>
public enum ReplicationSeat
{
    /// <summary>Host / 权威 World：Collect、Adhesion/Relocate、进 SimulationWorld。</summary>
    Authority = 0,

    /// <summary>客机本机：同一 <see cref="CharacterActor"/>，不 Collect、不进 World；预测卡肉 + Adhesion/Relocate 读只读 Proxy。</summary>
    Autonomous = 1,
}
