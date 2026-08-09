using UnityEngine;

/// <summary>敌人行为树资产；预设种树或 Custom SerializeReference 根 + Graph 布局。</summary>
[CreateAssetMenu(fileName = "EnemyBehaviorTree", menuName = "ACT/Enemy/Behavior Tree")]
public sealed class EnemyBehaviorTreeAsset : ScriptableObject, IEnemyBehaviorTreeAsset
{
    [SerializeField] EnemyBehaviorTreeKind kind = EnemyBehaviorTreeKind.MeleeChaseAttack;
    [SerializeReference] EnemyBehaviorNodeDef customRoot;
    [SerializeField] EnemyBehaviorGraphLayout graphLayout = new EnemyBehaviorGraphLayout();

    /// <summary>种树方式：预设或 Custom 序列化根。</summary>
    public EnemyBehaviorTreeKind Kind => kind;

    /// <summary>Custom 根节点定义（仅 Kind=Custom 时使用）。</summary>
    public EnemyBehaviorNodeDef CustomRoot => customRoot;

    /// <summary>Graph 画布布局；丢失不影响逻辑树。</summary>
    public EnemyBehaviorGraphLayout GraphLayout => graphLayout ??= new EnemyBehaviorGraphLayout();

    /// <summary>Editor：写入 Custom 根定义。</summary>
    public void SetCustomRootForEditor(EnemyBehaviorNodeDef root)
    {
        customRoot = root;
        if (customRoot != null)
        {
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(customRoot);
            EnemyBehaviorTreeGraphMapper.SyncLayout(GraphLayout, customRoot);
        }
    }

    /// <summary>Editor：切换 Kind。</summary>
    public void SetKindForEditor(EnemyBehaviorTreeKind value)
    {
        kind = value;
    }

    /// <summary>校验本资产（Custom 树结构 + 布局孤儿警告）。</summary>
    public EnemyBehaviorTreeValidationResult ValidateAsset() =>
        EnemyBehaviorTreeValidator.Validate(this);

    /// <summary>为 Graph 编辑准备：补 Guid/调试名并同步布局槽位。</summary>
    public void PrepareForGraphEditor()
    {
        if (kind != EnemyBehaviorTreeKind.Custom || customRoot == null)
            return;

        EnemyBehaviorTreeGraphMapper.EnsureStableIds(customRoot);
        EnemyBehaviorTreeGraphMapper.SyncLayout(GraphLayout, customRoot);
    }

    /// <inheritdoc />
    public IEnemyBehaviorRunner CreateRunner(in EnemyBehaviorBuildContext context)
    {
        IBehaviorNode root = kind switch
        {
            EnemyBehaviorTreeKind.ChaseOnly => EnemyBehaviorTreePresets.BuildChaseOnly(),
            EnemyBehaviorTreeKind.Kite => EnemyBehaviorTreePresets.BuildKite(),
            EnemyBehaviorTreeKind.Custom => BuildCustomRoot(),
            _ => EnemyBehaviorTreePresets.BuildMeleeChaseAttack(),
        };
        return new NativeBehaviorTreeRunner(root);
    }

    /// <summary>Custom 根为空时回退近战预设并告警，避免工厂炸掉。</summary>
    IBehaviorNode BuildCustomRoot()
    {
        if (customRoot == null)
        {
            Debug.LogWarning(
                $"EnemyBehaviorTreeAsset '{name}': Kind=Custom 但 customRoot 为空，回退 MeleeChaseAttack。",
                this);
            return EnemyBehaviorTreePresets.BuildMeleeChaseAttack();
        }

        return customRoot.Build();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        graphLayout ??= new EnemyBehaviorGraphLayout();
        if (kind != EnemyBehaviorTreeKind.Custom || customRoot == null)
            return;

        // 先校验再补 Id/布局，避免环上 Ensure 无意义地改数据；Ensure 本身已防环
        EnemyBehaviorTreeValidationResult result = EnemyBehaviorTreeValidator.Validate(this);
        for (int i = 0; i < result.Errors.Count; i++)
            Debug.LogError($"[EnemyBehaviorTree:{name}] {result.Errors[i]}", this);

        if (!result.IsValid)
            return;

        EnemyBehaviorTreeGraphMapper.EnsureStableIds(customRoot);
        EnemyBehaviorTreeGraphMapper.SyncLayout(graphLayout, customRoot);
    }
#endif
}

/// <summary>行为树资产种树方式。</summary>
public enum EnemyBehaviorTreeKind
{
    /// <summary>进战追击 + 冷却普攻（代码预设）。</summary>
    MeleeChaseAttack = 0,

    /// <summary>只追不打（代码预设）。</summary>
    ChaseOnly = 1,

    /// <summary>使用 SerializeReference customRoot（BT-2 Inspector 可编）。</summary>
    Custom = 2,

    /// <summary>风筝：过近后退 / 过远追击（代码预设）。</summary>
    Kite = 3,
}
