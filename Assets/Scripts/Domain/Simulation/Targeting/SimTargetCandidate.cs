/// <summary>确定性索敌使用的最小逻辑候选快照。</summary>
public readonly struct SimTargetCandidate
{
    /// <summary>创建一个已提交逻辑位置的目标候选。</summary>
    public SimTargetCandidate(
        SimActorId actorId,
        int teamId,
        int xMm,
        int zMm,
        bool isAlive)
    {
        ActorId = actorId;
        TeamId = teamId;
        XMm = xMm;
        ZMm = zMm;
        IsAlive = isAlive;
    }

    /// <summary>候选的稳定模拟身份。</summary>
    public SimActorId ActorId { get; }

    /// <summary>候选阵营。</summary>
    public int TeamId { get; }

    /// <summary>逻辑根世界 X，单位毫米。</summary>
    public int XMm { get; }

    /// <summary>逻辑根世界 Z，单位毫米。</summary>
    public int ZMm { get; }

    /// <summary>候选是否仍可被选择。</summary>
    public bool IsAlive { get; }
}
