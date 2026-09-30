using System;
using System.Collections.Generic;

/// <summary>复用场景/房间目标注册表读取实体圆盘，不以 Hurtbox 窗口或阵营过滤身体。</summary>
public sealed class CharacterBodyObstacleQuery : ISimBodyObstacleQuery
{
    readonly Func<IReadOnlyList<IHurtboxTarget>> _targets;
    readonly Predicate<IHurtboxTarget> _include;
    static readonly Comparison<SimBodyObstacle> StableOrder = CompareId;

    /// <summary>提供者必须返回当前房间的完整注册表；不缓存 Actor/Proxy 引用。</summary>
    public CharacterBodyObstacleQuery(Func<IReadOnlyList<IHurtboxTarget>> targets,
        Predicate<IHurtboxTarget> include = null)
    {
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        _include = include;
    }

    /// <inheritdoc />
    public void Collect(SimActorId selfId, List<SimBodyObstacle> results)
    {
        results.Clear();
        IReadOnlyList<IHurtboxTarget> targets = _targets();
        for (int i = 0; targets != null && i < targets.Count; i++)
        {
            if ((_include == null || _include(targets[i])) && targets[i] is ISimBodyObstacleSource source
                && source.TryGetBodyObstacle(out SimBodyObstacle body) && body.ActorId != selfId)
                results.Add(body);
        }
        results.Sort(StableOrder);
        for (int i = 1; i < results.Count; i++)
            if (results[i - 1].ActorId == results[i].ActorId)
                throw new InvalidOperationException($"Duplicate body actor id: {results[i].ActorId}");
    }

    static int CompareId(SimBodyObstacle a, SimBodyObstacle b) => a.ActorId.CompareTo(b.ActorId);
}
