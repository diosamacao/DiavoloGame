using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>连招图边：从节点的普通或 Perfect Cancel 通道派生到目标节点。</summary>
[Serializable]
public class ActionGraphEdge
{
    [SerializeField] string fromNodeId = string.Empty;
    [SerializeField] CancelWindowType routeKind = CancelWindowType.Normal;
    [SerializeField] string toNodeId = string.Empty;

    /// <summary>边起点节点。</summary>
    public string FromNodeId => fromNodeId;

    /// <summary>绑定的普通或 Perfect Cancel 通道。</summary>
    public CancelWindowType RouteKind => routeKind;

    /// <summary>边终点节点。</summary>
    public string ToNodeId => toNodeId;

}
