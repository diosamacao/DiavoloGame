using System.Collections.Generic;

/// <summary>
/// EditMode 测试用树构造器（非资产默认类型）。
/// 条件为 UE 风格装饰链：Condition → … → Task/Sequence；根外套 AggroGate。
/// </summary>
public static class EnemyBehaviorTreeDefFactory
{
    /// <summary>
    /// 测试：近战追打定义树。
    /// Attack = CooldownGate(Pulse) + WaitWhileInAction；Wait 必须在门控外。
    /// </summary>
    public static EnemyBehaviorNodeDef CreateMeleeChaseAttack()
    {
        var pulseBody = new SequenceNodeDef
        {
            NodeName = "PulseBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new StopMoveActionDef(),
                new PulseAttackActionDef(),
            },
        };

        var gatedPulse = new CooldownGateNodeDef
        {
            NodeName = "BasicAttackGate",
            CooldownId = EnemyCooldownIds.BasicAttack,
            CooldownFrames = 72,
            child = pulseBody,
        };

        EnemyBehaviorNodeDef pulse = NestDecorators(
            gatedPulse,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InAttackRangeConditionDef { NodeName = "InAttackRange", Distance = 2f },
            new IsCharacterStateConditionDef { NodeName = "IsLocomotion" });

        var attackBody = new SequenceNodeDef
        {
            NodeName = "AttackBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                pulse,
                new WaitWhileInActionActionDef { NodeName = "WaitWhileInAction" },
            },
        };

        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef
            {
                NodeName = "ChaseMove",
                Magnitude = 1f,
                StopDistance = 1.2f,
                FaceTarget = true,
            },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" });

        var selector = new SelectorNodeDef
        {
            NodeName = "MeleeRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                attackBody,
                chase,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };

        return WrapAggroGate(selector);
    }

    /// <summary>测试：只追不打定义树。</summary>
    public static EnemyBehaviorNodeDef CreateChaseOnly()
    {
        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef
            {
                NodeName = "ChaseMove",
                Magnitude = 1f,
                StopDistance = 1.2f,
            },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" });

        var selector = new SelectorNodeDef
        {
            NodeName = "ChaseOnlyRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                chase,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };

        return WrapAggroGate(selector);
    }

    /// <summary>
    /// 测试/样例：近战对峙循环（E-ST1）。
    /// Attack 不套 DistanceBand；Chase=OutsideFar；Strafe=InsideBand。
    /// 推荐拓扑（资产请在 Editor 按此搭，Agent 不改 .asset）：
    /// AggroGate → Selector( Attack | Chase(Band Far) | Strafe(Band Inside) | Idle )。
    /// </summary>
    public static EnemyBehaviorNodeDef CreateMeleeStanceLoop()
    {
        var pulseBody = new SequenceNodeDef
        {
            NodeName = "PulseBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new StopMoveActionDef(),
                new PulseAttackActionDef(),
            },
        };

        var gatedPulse = new CooldownGateNodeDef
        {
            NodeName = "BasicAttackGate",
            CooldownId = EnemyCooldownIds.BasicAttack,
            CooldownFrames = 72,
            child = pulseBody,
        };

        // Attack 高优先：只靠 InAttackRange，不套滞回 dwell
        EnemyBehaviorNodeDef pulse = NestDecorators(
            gatedPulse,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InAttackRangeConditionDef { NodeName = "InAttackRange", Distance = 2f },
            new IsCharacterStateConditionDef { NodeName = "IsLocomotion" });

        var attackBody = new SequenceNodeDef
        {
            NodeName = "AttackBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                pulse,
                new WaitWhileInActionActionDef { NodeName = "WaitWhileInAction" },
            },
        };

        // Chase：过远带 enter=3.5 / exit=2.8（exit&lt;enter）+ 最短驻留
        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef
            {
                NodeName = "ChaseMove",
                Magnitude = 1f,
                StopDistance = 1.2f,
                FaceTarget = true,
            },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new DistanceBandConditionDef
            {
                NodeName = "ChaseBand",
                Mode = DistanceBandMode.OutsideFar,
                EnterDistance = 3.5f,
                ExitDistance = 2.8f,
                MinDwellFrames = 6,
            });

        // Strafe：对峙区间 [2, 3.5]；与 Chase 滞回咬合，避免外沿每帧翻面
        EnemyBehaviorNodeDef strafe = NestDecorators(
            new StrafeAroundTargetActionDef
            {
                NodeName = "StrafeMove",
                SideSign = 1f,
                Magnitude = 0.35f,
            },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new DistanceBandConditionDef
            {
                NodeName = "StrafeBand",
                Mode = DistanceBandMode.InsideBand,
                EnterDistance = 2f,
                ExitDistance = 3.5f,
                MinDwellFrames = 6,
            });

        var selector = new SelectorNodeDef
        {
            NodeName = "StanceRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                attackBody,
                chase,
                strafe,
                new StopMoveActionDef { NodeName = "Idle" },
            },
        };

        return WrapAggroGate(selector);
    }

    /// <summary>测试：风筝定义树。</summary>
    public static EnemyBehaviorNodeDef CreateKite()
    {
        EnemyBehaviorNodeDef backOff = NestDecorators(
            new BackOffFromTargetActionDef { NodeName = "BackOffMove", Magnitude = 1f },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new DistanceLessEqualConditionDef { NodeName = "TooClose", Distance = 2.5f });

        EnemyBehaviorNodeDef chase = NestDecorators(
            new MoveTowardTargetActionDef { NodeName = "ChaseMove", Magnitude = 1f, StopDistance = 1.2f },
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

        var selector = new SelectorNodeDef
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

        return WrapAggroGate(selector);
    }

    /// <summary>根外套 AggroGate（进 10 / 脱 14）。</summary>
    public static EnemyBehaviorNodeDef WrapAggroGate(
        EnemyBehaviorNodeDef root,
        float enterRadius = 10f,
        float exitRadius = 14f) =>
        new AggroGateNodeDef
        {
            NodeName = "AggroGate",
            EnterRadius = enterRadius,
            ExitRadius = exitRadius,
            child = root,
        };

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
