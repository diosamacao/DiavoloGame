using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>WaitWhileInAction 行动定义：招式占用至离开 Action。</summary>
[Serializable]
public sealed class WaitWhileInActionActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new WaitWhileInActionAction());
}
