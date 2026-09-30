using System;

/// <summary>一次位移查询中冻结的实体圆盘；身体资格与受击/无敌窗口无关。</summary>
public readonly struct SimBodyObstacle
{
    public readonly SimActorId ActorId;
    public readonly SimVec2 PositionMm;
    public readonly int RadiusMm;

    /// <summary>仅为有效、已启用实体创建快照；半径为逻辑电机半径。</summary>
    public SimBodyObstacle(SimActorId actorId, SimVec2 positionMm, int radiusMm)
    {
        if (!actorId.IsValid) throw new ArgumentException("Body requires a stable actor id.", nameof(actorId));
        if (radiusMm < 0) throw new ArgumentOutOfRangeException(nameof(radiusMm));
        ActorId = actorId;
        PositionMm = positionMm;
        RadiusMm = radiusMm;
    }
}
