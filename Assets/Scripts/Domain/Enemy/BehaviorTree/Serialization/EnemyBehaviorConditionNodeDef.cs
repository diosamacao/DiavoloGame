using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>条件装饰定义基类（UE 风格：单子 + Abort Self）。</summary>
[Serializable]
public abstract class EnemyBehaviorConditionNodeDef : EnemyBehaviorNodeDef
{
    /// <summary>条件通过后进入的子树。</summary>
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <summary>构建子节点；缺省用 StopMove 占位（Validate 仍会报 child 为空）。</summary>
    protected IBehaviorNode BuildChild() =>
        child != null ? child.Build() : new StopMoveAction();
}
