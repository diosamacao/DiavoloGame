using System;
using System.Collections.Generic;

/// <summary>行为树 Graph 调色板条目与工厂。</summary>
public static class EnemyBehaviorNodeCatalog
{
    /// <summary>调色板分组。</summary>
    public enum Group
    {
        Composite,
        Decorator,
        Condition,
        Task,
    }

    /// <summary>单个可创建节点类型。</summary>
    public readonly struct Entry
    {
        public readonly Group Group;
        public readonly string DisplayName;
        public readonly Type DefType;

        public Entry(Group group, string displayName, Type defType)
        {
            Group = group;
            DisplayName = displayName;
            DefType = defType;
        }
    }

    static readonly Entry[] Entries =
    {
        new Entry(Group.Composite, "Selector", typeof(SelectorNodeDef)),
        new Entry(Group.Composite, "Sequence", typeof(SequenceNodeDef)),
        new Entry(Group.Composite, "RandomSelector", typeof(RandomSelectorNodeDef)),
        new Entry(Group.Decorator, "Inverter", typeof(InverterNodeDef)),
        new Entry(Group.Decorator, "Succeeder", typeof(SucceederNodeDef)),
        new Entry(Group.Decorator, "CooldownGate", typeof(CooldownGateNodeDef)),
        new Entry(Group.Decorator, "AggroGate", typeof(AggroGateNodeDef)),
        // Condition：运行时单子装饰；Graph 上叠到选中宿主顶部徽章
        new Entry(Group.Condition, "HasTarget", typeof(HasTargetConditionDef)),
        new Entry(Group.Condition, "InCombatAggro", typeof(InCombatAggroConditionDef)),
        new Entry(Group.Condition, "InAttackRange", typeof(InAttackRangeConditionDef)),
        new Entry(Group.Condition, "IsCharacterState", typeof(IsCharacterStateConditionDef)),
        new Entry(Group.Condition, "CooldownReady", typeof(CooldownReadyConditionDef)),
        new Entry(Group.Condition, "DistanceLessEqual", typeof(DistanceLessEqualConditionDef)),
        new Entry(Group.Condition, "DistanceGreater", typeof(DistanceGreaterConditionDef)),
        new Entry(Group.Condition, "DistanceBand", typeof(DistanceBandConditionDef)),
        new Entry(Group.Task, "StopMove", typeof(StopMoveActionDef)),
        new Entry(Group.Task, "MoveTowardTarget", typeof(MoveTowardTargetActionDef)),
        new Entry(Group.Task, "BackOffFromTarget", typeof(BackOffFromTargetActionDef)),
        new Entry(Group.Task, "StrafeAroundTarget", typeof(StrafeAroundTargetActionDef)),
        new Entry(Group.Task, "FaceTarget", typeof(FaceTargetActionDef)),
        new Entry(Group.Task, "RequestCombatAction", typeof(RequestCombatActionDef)),
        new Entry(Group.Task, "PulseDodge", typeof(PulseDodgeActionDef)),
        new Entry(Group.Task, "PulseHeavyAttack", typeof(PulseHeavyAttackActionDef)),
        new Entry(Group.Task, "PulseSkill", typeof(PulseSkillActionDef)),
        new Entry(Group.Task, "WaitWhileInAction", typeof(WaitWhileInActionActionDef)),
        new Entry(Group.Task, "WaitFrames", typeof(WaitFramesActionDef)),
    };

    /// <summary>全部调色板条目。</summary>
    public static IReadOnlyList<Entry> All => Entries;

    /// <summary>创建带默认 NodeName / Guid 的节点定义。</summary>
    public static EnemyBehaviorNodeDef Create(Type defType)
    {
        if (defType == null || !typeof(EnemyBehaviorNodeDef).IsAssignableFrom(defType))
            return new StopMoveActionDef();

        var def = (EnemyBehaviorNodeDef)Activator.CreateInstance(defType);
        EnemyBehaviorTreeGraphMapper.EnsureStableIds(def);
        return def;
    }

    /// <summary>复合节点可挂多个子。</summary>
    public static bool IsComposite(EnemyBehaviorNodeDef def) =>
        def is SelectorNodeDef || def is SequenceNodeDef || def is RandomSelectorNodeDef;

    /// <summary>条件装饰（UE 风格，单子 + Abort Self）。</summary>
    public static bool IsCondition(EnemyBehaviorNodeDef def) =>
        def is EnemyBehaviorConditionNodeDef;

    /// <summary>结构装饰（Inverter / Succeeder / CooldownGate / AggroGate）。</summary>
    public static bool IsStructuralDecorator(EnemyBehaviorNodeDef def) =>
        def is InverterNodeDef
        || def is SucceederNodeDef
        || def is CooldownGateNodeDef
        || def is AggroGateNodeDef;

    /// <summary>单子装饰拓扑（结构装饰或条件装饰）。</summary>
    public static bool IsDecorator(EnemyBehaviorNodeDef def) =>
        IsStructuralDecorator(def) || IsCondition(def);

    /// <summary>是否允许输出端口。</summary>
    public static bool HasOutput(EnemyBehaviorNodeDef def) =>
        IsComposite(def) || IsDecorator(def);

    /// <summary>分组显示名。</summary>
    public static string GroupLabel(Group group) => group switch
    {
        Group.Composite => "Composite",
        Group.Decorator => "Decorator",
        Group.Condition => "Condition",
        Group.Task => "Task",
        _ => "Other",
    };
}
