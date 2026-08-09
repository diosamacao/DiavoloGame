using System.Collections.Generic;

/// <summary>
/// EditMode 测试用树构造器（非资产默认类型）。
/// 条件为 UE 风格装饰链：Condition → … → Task/Sequence。
/// </summary>
public static class EnemyBehaviorTreeDefFactory
{
    /// <summary>测试：近战追打定义树（条件装饰套在分支上）。</summary>
    public static EnemyBehaviorNodeDef CreateMeleeChaseAttack()
    {
        var attackBody = new SequenceNodeDef
        {
            NodeName = "AttackBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new StopMoveActionDef(),
                new PulseAttackActionDef(),
            },
        };

        // 外层 → 内层：HasTarget → InRange → State → Cd → AttackBody
        EnemyBehaviorNodeDef attack = NestDecorators(
            attackBody,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InAttackRangeConditionDef { NodeName = "InAttackRange" },
            new IsCharacterStateConditionDef { NodeName = "IsLocomotion" },
            new CooldownReadyConditionDef { NodeName = "CooldownReady" });

        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef { NodeName = "ChaseMove" },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" });

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

    /// <summary>测试：只追不打定义树。</summary>
    public static EnemyBehaviorNodeDef CreateChaseOnly()
    {
        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef { NodeName = "ChaseMove" },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" });

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

    /// <summary>测试：风筝定义树。</summary>
    public static EnemyBehaviorNodeDef CreateKite()
    {
        EnemyBehaviorNodeDef backOff = NestDecorators(
            new BackOffFromTargetActionDef { NodeName = "BackOffMove" },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new DistanceLessEqualConditionDef { NodeName = "TooClose", Distance = 2.5f });

        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef { NodeName = "ChaseMove" },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new DistanceGreaterConditionDef { NodeName = "TooFar", Distance = 4f });

        var holdBody = new SequenceNodeDef
        {
            NodeName = "HoldBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new FaceTargetActionDef(),
                new StopMoveActionDef(),
            },
        };

        EnemyBehaviorNodeDef hold = NestDecorators(
            holdBody,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" });

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

    /// <summary>
    /// 将条件装饰由外到内套在叶子上：gates[0] 最外层。
    /// </summary>
    public static EnemyBehaviorNodeDef NestDecorators(
        EnemyBehaviorNodeDef inner,
        params EnemyBehaviorConditionNodeDef[] gatesOuterToInner)
    {
        if (gatesOuterToInner == null || gatesOuterToInner.Length == 0)
            return inner;

        var list = new List<EnemyBehaviorNodeDef>(gatesOuterToInner.Length);
        for (int i = 0; i < gatesOuterToInner.Length; i++)
        {
            if (gatesOuterToInner[i] != null)
                list.Add(gatesOuterToInner[i]);
        }

        return EnemyBehaviorTreeTopologyNormalizer.NestDecorators(inner, list);
    }
}
