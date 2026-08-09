using System.Collections.Generic;

/// <summary>把内置预设展开为可编辑的 SerializeReference 定义树。</summary>
public static class EnemyBehaviorTreeDefFactory
{
    /// <summary>近战追打定义树（与 EnemyBehaviorTreePresets.BuildMeleeChaseAttack 同构）。</summary>
    public static EnemyBehaviorNodeDef CreateMeleeChaseAttack()
    {
        var attack = new SequenceNodeDef
        {
            NodeName = "Attack",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InAttackRangeConditionDef(),
                new IsCharacterStateConditionDef(),
                new CooldownReadyConditionDef(),
                new StopMoveActionDef(),
                new PulseAttackActionDef(),
            },
        };

        var chase = new SequenceNodeDef
        {
            NodeName = "Chase",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InCombatAggroConditionDef(),
                new MoveTowardTargetActionDef(),
            },
        };

        return new SelectorNodeDef
        {
            NodeName = "MeleeRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                attack,
                chase,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };
    }

    /// <summary>只追不打定义树。</summary>
    public static EnemyBehaviorNodeDef CreateChaseOnly()
    {
        var chase = new SequenceNodeDef
        {
            NodeName = "Chase",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InCombatAggroConditionDef(),
                new MoveTowardTargetActionDef(),
            },
        };

        return new SelectorNodeDef
        {
            NodeName = "ChaseOnlyRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                chase,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };
    }

    /// <summary>风筝定义树（与 BuildKite 同构；阈值可在 Def 上改）。</summary>
    public static EnemyBehaviorNodeDef CreateKite()
    {
        var backOff = new SequenceNodeDef
        {
            NodeName = "BackOff",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InCombatAggroConditionDef(),
                new DistanceLessEqualConditionDef { Distance = 2.5f },
                new BackOffFromTargetActionDef(),
            },
        };

        var chase = new SequenceNodeDef
        {
            NodeName = "Chase",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InCombatAggroConditionDef(),
                new DistanceGreaterConditionDef { Distance = 4f },
                new MoveTowardTargetActionDef(),
            },
        };

        var hold = new SequenceNodeDef
        {
            NodeName = "Hold",
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new InCombatAggroConditionDef(),
                new FaceTargetActionDef(),
                new StopMoveActionDef(),
            },
        };

        return new SelectorNodeDef
        {
            NodeName = "KiteRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                backOff,
                chase,
                hold,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };
    }
}
