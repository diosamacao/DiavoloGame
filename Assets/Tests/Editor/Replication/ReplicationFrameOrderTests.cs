using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>冻结 Room 收包/采样先于 SimulationHost 固定步消费的 Unity 执行顺序。</summary>
public sealed class ReplicationFrameOrderTests
{
    /// <summary>Host 必须先写远端输入，再由 SimulationHost 消费下一逻辑帧。</summary>
    [Test]
    public void HostUpdate_RunsBeforeSimulationHostUpdate()
    {
        int roomOrder = GetExecutionOrder<ReplicationRoomHost>();
        int simulationOrder = GetExecutionOrder<SimulationHost>();

        Assert.That(roomOrder, Is.LessThan(simulationOrder));
        Assert.That(roomOrder, Is.EqualTo(-150));
        Assert.That(simulationOrder, Is.EqualTo(-100));
    }

    /// <summary>Client 必须先收权威 Tick 并采样，再由逻辑步回调推进本机预测。</summary>
    [Test]
    public void ClientUpdate_RunsBeforeSimulationHostUpdate()
    {
        int roomOrder = GetExecutionOrder<ReplicationRoomClient>();
        int simulationOrder = GetExecutionOrder<SimulationHost>();

        Assert.That(roomOrder, Is.LessThan(simulationOrder));
        Assert.That(roomOrder, Is.EqualTo(-150));
        Assert.That(simulationOrder, Is.EqualTo(-100));
    }

    /// <summary>读取类型显式声明的 Unity 默认执行顺序；缺少声明视为测试失败。</summary>
    static int GetExecutionOrder<T>()
    {
        DefaultExecutionOrder attribute = typeof(T).GetCustomAttribute<DefaultExecutionOrder>();
        Assert.That(attribute, Is.Not.Null, $"{typeof(T).Name} 必须显式声明 DefaultExecutionOrder。");
        return attribute.order;
    }
}
