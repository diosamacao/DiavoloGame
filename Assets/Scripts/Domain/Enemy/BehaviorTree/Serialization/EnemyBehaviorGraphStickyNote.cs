using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Graph 画布便签（仅编辑器）。</summary>
[Serializable]
public sealed class EnemyBehaviorGraphStickyNote
{
    public string text;
    public Vector2 position;
    public Vector2 size = new Vector2(180f, 80f);
}
