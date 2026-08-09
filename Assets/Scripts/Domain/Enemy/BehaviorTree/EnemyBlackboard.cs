using System.Text;
using UnityEngine;

/// <summary>单敌人运行时黑板；感知由 Brain 填入，决策只写本帧输出槽。</summary>
public sealed class EnemyBlackboard
{
    readonly StringBuilder _debugPath = new StringBuilder(128);

    /// <summary>数值配置（只读使用）。</summary>
    public EnemyBrainProfile Profile;

    /// <summary>为 true 时 NamedNode 记录本帧路径。</summary>
    public bool DebugEnabled;

    /// <summary>路径方向查询；可空则用 PlanarDirection。</summary>
    public IEnemyPathQuery PathQuery;

    /// <summary>通用冷却表（Brain TickDown；条件/装饰只读或 Gate 写入）。</summary>
    public EnemyCooldownTable Cooldowns { get; } = new EnemyCooldownTable();

    /// <summary>是否有有效目标。</summary>
    public bool HasTarget;

    /// <summary>与目标的水平距离。</summary>
    public float PlanarDistance;

    /// <summary>指向目标的水平单位方向。</summary>
    public Vector3 PlanarDirection;

    /// <summary>本帧追击转向（寻路预留；首版=PlanarDirection）。</summary>
    public Vector3 PathDirection;

    /// <summary>角色当前状态。</summary>
    public CharacterStateType CharacterState;

    /// <summary>是否已死亡。</summary>
    public bool IsDead;

    /// <summary>仇恨滞回：进 AggroRadius 置真，出 LoseAggroRadius 置假。</summary>
    public bool IsAggroed;

    /// <summary>Brain：攻击脉冲后等待进入 Action 的确认期（阻塞 basic_attack 就绪）。</summary>
    public bool AttackConfirmPending;

    /// <summary>本帧期望移动（局部前进轴惯例：y&gt;0 前进）。</summary>
    public Vector2 MoveDesire;

    /// <summary>本帧是否请求攻击脉冲。</summary>
    public bool AttackPulse;

    /// <summary>本帧是否请求闪避脉冲。</summary>
    public bool DodgePulse;

    /// <summary>本帧是否请求重击脉冲。</summary>
    public bool HeavyAttackPulse;

    /// <summary>本帧是否请求特殊/技能脉冲。</summary>
    public bool SkillPulse;

    /// <summary>本帧是否请求刷新面向目标。</summary>
    public bool FaceTargetRequested;

    /// <summary>本帧调试路径（仅 DebugEnabled 时有内容）。</summary>
    public string DebugPath => _debugPath.ToString();

    /// <summary>帧初清空决策输出，避免多节点残留。</summary>
    public void ResetFrameOutputs()
    {
        MoveDesire = Vector2.zero;
        AttackPulse = false;
        DodgePulse = false;
        HeavyAttackPulse = false;
        SkillPulse = false;
        FaceTargetRequested = false;
        _debugPath.Length = 0;
    }

    /// <summary>NamedNode 追加「名:状态」片段。</summary>
    public void AppendDebug(string nodeName, BehaviorStatus status)
    {
        if (!DebugEnabled)
            return;
        if (_debugPath.Length > 0)
            _debugPath.Append(" > ");
        _debugPath.Append(nodeName).Append(':').Append(status);
    }
}
