/// <summary>本机进程在网络拓扑中的宿主角色；Dedicated 不得由 Listen 开关冒充。</summary>
public enum NetProcessRole : byte
{
    /// <summary>远端客户端：只预测自己并跟快照。</summary>
    Client = 0,

    /// <summary>Listen Server：同进程既有权威世界也有本机玩家。</summary>
    ListenServer = 1,

    /// <summary>无本地玩家的独立权威宿主。</summary>
    DedicatedServer = 2,
}
