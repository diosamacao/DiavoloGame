using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证命中键只按逻辑帧与稳定模拟身份形成确定性顺序。</summary>
public sealed class SimHitKeyTests
{
    /// <summary>乱序命中必须按 frame、attacker、hitbox、target 的固定字典序排列。</summary>
    [Test]
    public void Sort_OrdersByStableSimulationFields()
    {
        var keys = new List<SimHitKey>
        {
            Key(frame: 1, attacker: 1, action: 1, hitbox: 0, target: 2),
            Key(frame: 0, attacker: 2, action: 1, hitbox: 0, target: 1),
            Key(frame: 0, attacker: 1, action: 1, hitbox: 1, target: 2),
            Key(frame: 0, attacker: 1, action: 1, hitbox: 0, target: 3),
            Key(frame: 0, attacker: 1, action: 1, hitbox: 0, target: 2)
        };

        keys.Sort();

        Assert.That(keys[0], Is.EqualTo(Key(0, 1, 1, 0, 2)));
        Assert.That(keys[1], Is.EqualTo(Key(0, 1, 1, 0, 3)));
        Assert.That(keys[2], Is.EqualTo(Key(0, 1, 1, 1, 2)));
        Assert.That(keys[3], Is.EqualTo(Key(0, 2, 1, 0, 1)));
        Assert.That(keys[4], Is.EqualTo(Key(1, 1, 1, 0, 2)));
    }

    /// <summary>相同会话、Hitbox 与目标生成相等键，不需要 Unity InstanceId。</summary>
    [Test]
    public void Equality_UsesActionSessionAndSimActorIds()
    {
        SimHitKey first = Key(7, 3, 11, 2, 9);
        SimHitKey same = Key(7, 3, 11, 2, 9);
        SimHitKey nextAction = Key(7, 3, 12, 2, 9);

        Assert.That(first, Is.EqualTo(same));
        Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        Assert.That(first, Is.Not.EqualTo(nextAction));
    }

    /// <summary>构造测试命中键，所有 Actor Id 都使用有效正整数。</summary>
    static SimHitKey Key(long frame, int attacker, int action, int hitbox, int target) =>
        new(
            frame,
            new SimActorId(attacker),
            action,
            hitbox,
            new SimActorId(target));
}
