using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>可序列化行为树节点定义；Build 为运行时 IBehaviorNode（BT-2 Custom）。</summary>
[Serializable]
public abstract class EnemyBehaviorNodeDef
{
    /// <summary>自定义节点名（画布标题 / 运行路径 / Gizmo）；空则用类型短名。</summary>
    [FormerlySerializedAs("debugName")]
    [SerializeField] string nodeName;

    /// <summary>系统分配的稳定 id；仅供 Graph 布局/扁平表对节点，勿手填。</summary>
    [SerializeField, HideInInspector] string nodeGuid;

    /// <summary>自定义节点名；显示与调试路径共用，不是可选的旁路字段。</summary>
    public string NodeName
    {
        get => nodeName;
        set => nodeName = value;
    }

    /// <summary>系统分配的稳定 id（HideInInspector）；布局与 Flatten 用，与战斗逻辑无关。</summary>
    public string NodeGuid
    {
        get => nodeGuid;
        set => nodeGuid = value;
    }

    /// <summary>构建运行时节点。</summary>
    public abstract IBehaviorNode Build();

    /// <summary>包一层 NamedNode：优先 NodeName，否则类型短名（画布与 LastDebugPath 对齐）。</summary>
    protected IBehaviorNode Wrap(IBehaviorNode node)
    {
        if (node == null)
            return new StopMoveAction();
        string name = string.IsNullOrEmpty(nodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(this)
            : nodeName;
        return new NamedNode(name, node);
    }
}
