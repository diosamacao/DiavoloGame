/// <summary>权威招架窗接触：本机预测镜像 Success 与同一套卡肉帧。</summary>
public readonly struct AssistParryContact
{
    /// <summary>记录被接触的玩家权威 Id 与本刀卡肉帧。</summary>
    public AssistParryContact(SimActorId targetId, int hitStopFrames)
    {
        TargetId = targetId;
        HitStopFrames = hitStopFrames > 0 ? hitStopFrames : 0;
    }

    /// <summary>招架窗所属玩家。</summary>
    public SimActorId TargetId { get; }

    /// <summary>与权威写入相同的卡肉逻辑帧；0 表示本刀不冻。</summary>
    public int HitStopFrames { get; }
}
