/// <summary>复制快照中的角色类别；用于生成与插值，不参与命中身份。</summary>
public enum ReplicationActorKind : byte
{
    /// <summary>玩家控制角色。</summary>
    Player = 0,

    /// <summary>敌人 / AI 角色。</summary>
    Enemy = 1,
}
