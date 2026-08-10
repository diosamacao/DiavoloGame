using System.Collections.Generic;

/// <summary>
/// EditMode 测试用树构造器（非资产默认类型）。
/// 条件为 UE 风格装饰链：Condition → … → Task/Sequence；根外套 AggroGate。
/// 出招叶为 RequestCombatAction / RandomSelector（E-REQ2，无 AttackPulse）。
/// </summary>
public static class EnemyBehaviorTreeDefFactory
{
    /// <summary>
    /// 测试：近战追打定义树。
    /// Attack = CooldownGate(Request) + WaitWhileInAction；Wait 必须在门控外。
    /// </summary>
    public static EnemyBehaviorNodeDef CreateMeleeChaseAttack(string entryNodeId = "Entry_Basic")
    {
        var attackBody = CreateGatedRequestAttackBody(entryNodeId);

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
    /// 测试/样例：近战对峙循环。
    /// Attack（贴近+CD 就绪）→ Strafe（CD 中对峙）←→ Chase（CD 毕且未贴身）→ Idle。
    /// 推荐拓扑（资产请在 Editor 按此搭，Agent 不改 .asset）：
    /// AggroGate → Selector( Attack | Chase(CdReady) | Strafe(CdNotReady) | Idle )。
    /// </summary>
    public static EnemyBehaviorNodeDef CreateMeleeStanceLoop(string entryNodeId = "Entry_Basic")
    {
        var attackBody = CreateGatedRequestAttackBody(entryNodeId);

        // CD 结束后追击，直到进入 Attack 距离
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
            new CooldownReadyConditionDef
            {
                NodeName = "CdReadyChase",
                CooldownId = EnemyCooldownIds.BasicAttack,
            });

        // CD 期间对峙左右走，不追击
        EnemyBehaviorNodeDef strafe = NestDecorators(
            new StrafeAroundTargetActionDef
            {
                NodeName = "StrafeMove",
                SideSign = 1f,
                Magnitude = 0.35f,
            },
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InCombatAggroConditionDef { NodeName = "InAggro" },
            new CooldownNotReadyConditionDef
            {
                NodeName = "CdBusyStance",
                CooldownId = EnemyCooldownIds.BasicAttack,
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

    /// <summary>
    /// E-REQ2 样例：权重招式池（Request 叶，非 Pulse）。
    /// 默认权重 3:2:1 → Entry_A / Entry_B / Entry_C。
    /// </summary>
    public static EnemyBehaviorNodeDef CreateCombatPool(
        string entryA = "Entry_A",
        string entryB = "Entry_B",
        string entryC = "Entry_C")
    {
        var pool = new RandomSelectorNodeDef
        {
            NodeName = "CombatPool",
            children = new List<EnemyBehaviorNodeDef>
            {
                new RequestCombatActionDef { NodeName = "ReqA", EntryNodeId = entryA },
                new RequestCombatActionDef { NodeName = "ReqB", EntryNodeId = entryB },
                new RequestCombatActionDef { NodeName = "ReqC", EntryNodeId = entryC },
            },
            weights = new List<float> { 3f, 2f, 1f },
        };

        var requestBody = new SequenceNodeDef
        {
            NodeName = "RequestBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new StopMoveActionDef { NodeName = "StopBeforeRequest" },
                pool,
            },
        };

        var gated = new CooldownGateNodeDef
        {
            NodeName = "BasicAttackGate",
            CooldownId = EnemyCooldownIds.BasicAttack,
            CooldownSeconds = 1.2f,
            child = requestBody,
        };

        EnemyBehaviorNodeDef gatedRequest = NestDecorators(
            gated,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InAttackRangeConditionDef { NodeName = "InAttackRange", Distance = 2f },
            new IsCharacterStateConditionDef { NodeName = "IsLocomotion" });

        var attackBody = new SequenceNodeDef
        {
            NodeName = "AttackBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                gatedRequest,
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
            NodeName = "CombatPoolRoot",
            children = new List<EnemyBehaviorNodeDef>
            {
                attackBody,
                chase,
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

    /// <summary>CooldownGate(Stop+Request) + WaitWhileInAction 攻击支。</summary>
    static EnemyBehaviorNodeDef CreateGatedRequestAttackBody(string entryNodeId)
    {
        var requestBody = new SequenceNodeDef
        {
            NodeName = "RequestBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                new StopMoveActionDef { NodeName = "StopBeforeRequest" },
                new RequestCombatActionDef
                {
                    NodeName = "RequestCombat",
                    EntryNodeId = entryNodeId,
                },
            },
        };

        var gatedRequest = new CooldownGateNodeDef
        {
            NodeName = "BasicAttackGate",
            CooldownId = EnemyCooldownIds.BasicAttack,
            CooldownSeconds = 1.2f,
            child = requestBody,
        };

        EnemyBehaviorNodeDef request = NestDecorators(
            gatedRequest,
            new HasTargetConditionDef { NodeName = "HasTarget" },
            new InAttackRangeConditionDef { NodeName = "InAttackRange", Distance = 2f },
            new IsCharacterStateConditionDef { NodeName = "IsLocomotion" });

        return new SequenceNodeDef
        {
            NodeName = "AttackBody",
            children = new List<EnemyBehaviorNodeDef>
            {
                request,
                new WaitWhileInActionActionDef { NodeName = "WaitWhileInAction" },
            },
        };
    }
}
