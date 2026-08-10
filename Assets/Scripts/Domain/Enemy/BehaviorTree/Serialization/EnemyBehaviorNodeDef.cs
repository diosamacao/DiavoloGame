using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>可序列化行为树节点定义；Build 为运行时 IBehaviorNode（BT-2 Custom）。</summary>
[Serializable]
public abstract class EnemyBehaviorNodeDef
{
    /// <summary>自定义节点名（画布标题 / 运行路径 / Gizmo）；空则用类型短名。</summary>
    [FormerlySerializedAs("debugName")]
    [SerializeField] string nodeName;

    /// <summary>系统分配的稳定 id；仅供 Graph 布局/扁平表对节点，勿手填。</summary>
    [SerializeField, HideInInspector] string nodeGuid;

    /// <summary>自定义节点名；显示与调试路径共用，不是可选的旁路字段。</summary>
    public string NodeName
    {
        get => nodeName;
        set => nodeName = value;
    }

    /// <summary>系统分配的稳定 id（HideInInspector）；布局与 Flatten 用，与战斗逻辑无关。</summary>
    public string NodeGuid
    {
        get => nodeGuid;
        set => nodeGuid = value;
    }

    /// <summary>构建运行时节点。</summary>
    public abstract IBehaviorNode Build();

    /// <summary>包一层 NamedNode：优先 NodeName，否则类型短名（画布与 LastDebugPath 对齐）。</summary>
    protected IBehaviorNode Wrap(IBehaviorNode node)
    {
        if (node == null)
            return new StopMoveAction();
        string name = string.IsNullOrEmpty(nodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(this)
            : nodeName;
        return new NamedNode(name, node);
    }
}

/// <summary>Selector 定义。</summary>
[Serializable]
public sealed class SelectorNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public List<EnemyBehaviorNodeDef> children = new List<EnemyBehaviorNodeDef>();

    /// <inheritdoc />
    public override IBehaviorNode Build()
    {
        var built = new IBehaviorNode[children != null ? children.Count : 0];
        for (int i = 0; i < built.Length; i++)
            built[i] = children[i] != null ? children[i].Build() : new StopMoveAction();
        return Wrap(new SelectorNode(built));
    }
}

/// <summary>Sequence 定义。</summary>
[Serializable]
public sealed class SequenceNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public List<EnemyBehaviorNodeDef> children = new List<EnemyBehaviorNodeDef>();

    /// <inheritdoc />
    public override IBehaviorNode Build()
    {
        var built = new IBehaviorNode[children != null ? children.Count : 0];
        for (int i = 0; i < built.Length; i++)
            built[i] = children[i] != null ? children[i].Build() : new StopMoveAction();
        return Wrap(new SequenceNode(built));
    }
}

/// <summary>Inverter 定义。</summary>
[Serializable]
public sealed class InverterNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new InverterNode(child != null ? child.Build() : new StopMoveAction()));
}

/// <summary>Succeeder 定义。</summary>
[Serializable]
public sealed class SucceederNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new SucceederNode(child != null ? child.Build() : new StopMoveAction()));
}

/// <summary>CooldownGate 定义；子节点 Success 时写入冷却。</summary>
[Serializable]
public sealed class CooldownGateNodeDef : EnemyBehaviorNodeDef
{
    [SerializeField] string cooldownId = EnemyCooldownIds.Dodge;
    [SerializeField] int cooldownFrames = 60;
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <summary>冷却 id（Graph Inspector 可编）。</summary>
    public string CooldownId
    {
        get => cooldownId;
        set => cooldownId = value;
    }

    /// <summary>子节点 Success 后写入的冷却帧数。</summary>
    public int CooldownFrames
    {
        get => cooldownFrames;
        set => cooldownFrames = Mathf.Max(0, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new CooldownGateNode(
            cooldownId,
            cooldownFrames,
            child != null ? child.Build() : new StopMoveAction()));
}

/// <summary>AggroGate 定义：维护 IsAggroed 滞回后 Tick 子树。</summary>
[Serializable]
public sealed class AggroGateNodeDef : EnemyBehaviorNodeDef
{
    [SerializeField] float enterRadius = 10f;
    [SerializeField] float exitRadius = 14f;
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <summary>进入仇恨的水平距离。</summary>
    public float EnterRadius
    {
        get => enterRadius;
        set => enterRadius = Mathf.Max(0f, value);
    }

    /// <summary>脱离仇恨的水平距离（至少等于 enter）。</summary>
    public float ExitRadius
    {
        get => exitRadius;
        set => exitRadius = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new AggroGateNode(
            enterRadius,
            exitRadius,
            child != null ? child.Build() : new StopMoveAction()));
}

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

/// <summary>HasTarget 条件装饰定义。</summary>
[Serializable]
public sealed class HasTargetConditionDef : EnemyBehaviorConditionNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new HasTargetCondition(BuildChild()));
}

/// <summary>InCombatAggro 条件装饰定义。</summary>
[Serializable]
public sealed class InCombatAggroConditionDef : EnemyBehaviorConditionNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InCombatAggroCondition(BuildChild()));
}

/// <summary>InAttackRange 条件装饰定义；距离在节点上，不读 Profile。</summary>
[Serializable]
public sealed class InAttackRangeConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 2f;

    /// <summary>攻击距离上限（米）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InAttackRangeCondition(distance, BuildChild()));
}

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

/// <summary>CooldownReady 条件装饰定义。</summary>
[Serializable]
public sealed class CooldownReadyConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] string cooldownId = EnemyCooldownIds.BasicAttack;

    /// <summary>冷却 id。</summary>
    public string CooldownId
    {
        get => cooldownId;
        set => cooldownId = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new CooldownReadyCondition(cooldownId, BuildChild()));
}

/// <summary>DistanceLessEqual 条件装饰定义。</summary>
[Serializable]
public sealed class DistanceLessEqualConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 2f;

    /// <summary>距离上限（米）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceLessEqualCondition(distance, BuildChild()));
}

/// <summary>DistanceGreater 条件装饰定义。</summary>
[Serializable]
public sealed class DistanceGreaterConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 4f;

    /// <summary>距离下限（米，严格大于）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceGreaterCondition(distance, BuildChild()));
}

/// <summary>DistanceBand 滞回条件装饰定义；Chase/Strafe 带，Attack 勿套。</summary>
[Serializable]
public sealed class DistanceBandConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] DistanceBandMode mode = DistanceBandMode.OutsideFar;
    [SerializeField] float enterDistance = 4f;
    [SerializeField] float exitDistance = 3f;
    [SerializeField] int minDwellFrames = 6;

    /// <summary>滞回带模式。</summary>
    public DistanceBandMode Mode
    {
        get => mode;
        set => mode = value;
    }

    /// <summary>进入本支的距离阈值（米）。</summary>
    public float EnterDistance
    {
        get => enterDistance;
        set => enterDistance = Mathf.Max(0f, value);
    }

    /// <summary>离开本支的距离阈值（米）；Chase/OutsideFar 宜 &lt; enter。</summary>
    public float ExitDistance
    {
        get => exitDistance;
        set => exitDistance = Mathf.Max(0f, value);
    }

    /// <summary>最短驻留逻辑帧；满后才允许因距离翻面失败。</summary>
    public int MinDwellFrames
    {
        get => minDwellFrames;
        set => minDwellFrames = Mathf.Max(0, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceBandCondition(mode, enterDistance, exitDistance, minDwellFrames, BuildChild()));
}

/// <summary>StopMove 行动定义。</summary>
[Serializable]
public sealed class StopMoveActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StopMoveAction());
}

/// <summary>MoveTowardTarget 行动定义；幅度/停步在节点上。</summary>
[Serializable]
public sealed class MoveTowardTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField, Range(0f, 1f)] float magnitude = 1f;
    [SerializeField] float stopDistance = 1.2f;
    [SerializeField] bool faceTarget = true;

    /// <summary>本地前进轴幅度。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <summary>贴身停步距离（米）。</summary>
    public float StopDistance
    {
        get => stopDistance;
        set => stopDistance = Mathf.Max(0f, value);
    }

    /// <summary>追击时是否请求面向目标。</summary>
    public bool FaceTarget
    {
        get => faceTarget;
        set => faceTarget = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new MoveTowardTargetAction(magnitude, stopDistance, faceTarget));
}

/// <summary>BackOffFromTarget 行动定义。</summary>
[Serializable]
public sealed class BackOffFromTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField, Range(0f, 1f)] float magnitude = 1f;

    /// <summary>后退幅度。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new BackOffFromTargetAction(magnitude));
}

/// <summary>StrafeAroundTarget 行动定义。</summary>
[Serializable]
public sealed class StrafeAroundTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float sideSign = 1f;
    [SerializeField, Range(0f, 1f)] float magnitude = 0.35f;

    /// <summary>侧移符号：&gt;0 右，&lt;0 左。</summary>
    public float SideSign
    {
        get => sideSign;
        set => sideSign = value >= 0f ? 1f : -1f;
    }

    /// <summary>侧移幅度（宜小于 RunThreshold）。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StrafeAroundTargetAction(sideSign, magnitude));
}

/// <summary>FaceTarget 行动定义。</summary>
[Serializable]
public sealed class FaceTargetActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new FaceTargetAction());
}

/// <summary>PulseAttack 行动定义。</summary>
[Serializable]
public sealed class PulseAttackActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new PulseAttackAction());
}

/// <summary>PulseDodge 行动定义。</summary>
[Serializable]
public sealed class PulseDodgeActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new PulseDodgeAction());
}

/// <summary>PulseHeavyAttack 行动定义。</summary>
[Serializable]
public sealed class PulseHeavyAttackActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new PulseHeavyAttackAction());
}

/// <summary>PulseSkill 行动定义。</summary>
[Serializable]
public sealed class PulseSkillActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new PulseSkillAction());
}

/// <summary>WaitWhileInAction 行动定义：招式占用至离开 Action。</summary>
[Serializable]
public sealed class WaitWhileInActionActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new WaitWhileInActionAction());
}

/// <summary>WaitFrames 行动定义。</summary>
[Serializable]
public sealed class WaitFramesActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] int durationFrames = 30;

    /// <summary>等待逻辑帧数。</summary>
    public int DurationFrames
    {
        get => durationFrames;
        set => durationFrames = Mathf.Max(1, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new WaitFramesAction(durationFrames));
}
