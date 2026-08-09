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

/// <summary>HasTarget 条件定义。</summary>
[Serializable]
public sealed class HasTargetConditionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new HasTargetCondition());
}

/// <summary>InCombatAggro 条件定义。</summary>
[Serializable]
public sealed class InCombatAggroConditionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InCombatAggroCondition());
}

/// <summary>InAttackRange 条件定义。</summary>
[Serializable]
public sealed class InAttackRangeConditionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InAttackRangeCondition());
}

/// <summary>IsCharacterState 条件定义。</summary>
[Serializable]
public sealed class IsCharacterStateConditionDef : EnemyBehaviorNodeDef
{
    [SerializeField] CharacterStateType expected = CharacterStateType.Locomotion;

    /// <summary>期望角色状态。</summary>
    public CharacterStateType Expected
    {
        get => expected;
        set => expected = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new IsCharacterStateCondition(expected));
}

/// <summary>CooldownReady 条件定义。</summary>
[Serializable]
public sealed class CooldownReadyConditionDef : EnemyBehaviorNodeDef
{
    [SerializeField] string cooldownId = EnemyCooldownIds.BasicAttack;

    /// <summary>冷却 id。</summary>
    public string CooldownId
    {
        get => cooldownId;
        set => cooldownId = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new CooldownReadyCondition(cooldownId));
}

/// <summary>DistanceLessEqual 条件定义。</summary>
[Serializable]
public sealed class DistanceLessEqualConditionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float distance = 2f;

    /// <summary>距离上限（米）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new DistanceLessEqualCondition(distance));
}

/// <summary>DistanceGreater 条件定义。</summary>
[Serializable]
public sealed class DistanceGreaterConditionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float distance = 4f;

    /// <summary>距离下限（米，严格大于）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new DistanceGreaterCondition(distance));
}

/// <summary>StopMove 行动定义。</summary>
[Serializable]
public sealed class StopMoveActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StopMoveAction());
}

/// <summary>MoveTowardTarget 行动定义。</summary>
[Serializable]
public sealed class MoveTowardTargetActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new MoveTowardTargetAction());
}

/// <summary>BackOffFromTarget 行动定义。</summary>
[Serializable]
public sealed class BackOffFromTargetActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new BackOffFromTargetAction());
}

/// <summary>StrafeAroundTarget 行动定义。</summary>
[Serializable]
public sealed class StrafeAroundTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float sideSign = 1f;

    /// <summary>侧移符号：&gt;0 右，&lt;0 左。</summary>
    public float SideSign
    {
        get => sideSign;
        set => sideSign = value >= 0f ? 1f : -1f;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StrafeAroundTargetAction(sideSign));
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
