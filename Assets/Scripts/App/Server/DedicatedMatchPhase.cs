/// <summary>Dedicated 对局生命周期；Lobby/Playing 可 Join，Ending 之后拒收。</summary>
public enum DedicatedMatchPhase : byte
{
    /// <summary>空房等待第一名玩家。</summary>
    Lobby = 0,

    /// <summary>已接纳至少一名玩家，本 Poll 末转入 Playing。</summary>
    Starting = 1,

    /// <summary>权威步进并按连接下发 ReplicationFrame。</summary>
    Playing = 2,

    /// <summary>向仍在线连接可靠下发 MatchEnd。</summary>
    Ending = 3,

    /// <summary>清权威 Actor 后回到 Lobby。</summary>
    Cleanup = 4,
}
