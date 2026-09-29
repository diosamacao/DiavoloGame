using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>FaceTarget 行动定义。</summary>
[Serializable]
public sealed class FaceTargetActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new FaceTargetAction());
}
