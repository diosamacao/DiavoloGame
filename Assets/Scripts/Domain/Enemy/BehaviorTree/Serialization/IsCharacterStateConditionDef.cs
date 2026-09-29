using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>IsCharacterState 条件装饰定义。</summary>
[Serializable]
public sealed class IsCharacterStateConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] CharacterStateType expected = CharacterStateType.Locomotion;

    /// <summary>期望角色状态。</summary>
    public CharacterStateType Expected
    {
        get => expected;
        set => expected = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new IsCharacterStateCondition(expected, BuildChild()));
}
