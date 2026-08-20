/// <summary>本机在房间中的进程角色；单机默认 Listen Host。</summary>
public enum ReplicationRole
{
    /// <summary>本机组合 ServerRuntime + LocalClient；权威只在 ServerRuntime。</summary>
    ListenHost = 0,

    /// <summary>本机只预测自己并跟快照；不刷怪、不 Collect。</summary>
    Client = 1,

    /// <summary>无本地玩家的独立权威宿主；由 DedicatedServerBootstrap 启动，不得用 ListenHost 冒充。</summary>
    DedicatedServer = 2,
}
