/// <summary>本机在 NS5 房间中的角色；单机默认 Listen Host。</summary>
public enum ReplicationRole
{
    /// <summary>本机跑权威 SimulationWorld；可一人进关或等人加入。</summary>
    ListenHost = 0,

    /// <summary>本机只预测自己并跟快照；不刷怪、不 Collect。</summary>
    Client = 1,
}
