using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单个节点在 Graph 画布上的布局。</summary>
[Serializable]
public sealed class EnemyBehaviorGraphNodeLayout
{
    public string nodeGuid;
    public Vector2 position;
    public bool collapsed;
}
