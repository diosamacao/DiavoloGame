using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>冻结 Listen / Client 收包先于 SimulationHost 固定步消费的 Unity 执行顺序。</summary>
public sealed class ReplicationFrameOrderTests
{
    /// <summary>Listen 组合必须先于 SimulationHost 泵本机命令与权威 Poll。</summary>
    [Test]
    public void ListenUpdate_RunsBeforeSimulationHostUpdate()
    {
        int listenOrder = GetExecutionOrder<ListenServerBootstrap>();
        int simulationOrder = GetExecutionOrder<SimulationHost>();

        Assert.That(listenOrder, Is.LessThan(simulationOrder));
        Assert.That(listenOrder, Is.EqualTo(-210));
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
