using System;
using UnityEngine;

/// <summary>敌人行为树资产；仅 SerializeReference 根 + Graph 布局，无代码预设 Kind。</summary>
[CreateAssetMenu(fileName = "EnemyBehaviorTree", menuName = "ACT/Enemy/Behavior Tree")]
public sealed class EnemyBehaviorTreeAsset : ScriptableObject, IEnemyBehaviorTreeAsset
{
    [SerializeReference] EnemyBehaviorNodeDef customRoot;
    /// <summary>画布坐标缓存；仅 Graph 编辑器读写，Inspector 不展示以免误改 guid。</summary>
    [HideInInspector]
    [SerializeField] EnemyBehaviorGraphLayout graphLayout = new EnemyBehaviorGraphLayout();

    /// <summary>行为树根节点定义（须在 Graph/Inspector 中手动配置）。</summary>
    public EnemyBehaviorNodeDef CustomRoot => customRoot;

    /// <summary>Graph 画布布局；丢失不影响逻辑树。</summary>
    public EnemyBehaviorGraphLayout GraphLayout => graphLayout ??= new EnemyBehaviorGraphLayout();

    /// <summary>Editor：写入根定义。</summary>
    public void SetCustomRootForEditor(EnemyBehaviorNodeDef root)
    {
        customRoot = root;
        if (customRoot != null)
        {
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(customRoot);
            EnemyBehaviorTreeGraphMapper.SyncLayout(GraphLayout, customRoot);
        }
    }

    /// <summary>校验本资产（须有根 + 结构合法）。</summary>
    public EnemyBehaviorTreeValidationResult ValidateAsset() =>
        EnemyBehaviorTreeValidator.Validate(this);

    /// <summary>为 Graph 编辑准备：迁移叶子条件、补 Guid/节点名并同步布局。</summary>
    public void PrepareForGraphEditor()
    {
        if (customRoot == null)
            return;

        EnemyBehaviorTreeTopologyNormalizer.Normalize(customRoot);
        EnemyBehaviorTreeGraphMapper.EnsureStableIds(customRoot);
        EnemyBehaviorTreeGraphMapper.SyncLayout(GraphLayout, customRoot);
    }

    /// <inheritdoc />
    public IEnemyBehaviorRunner CreateRunner(in EnemyBehaviorBuildContext context)
    {
        if (customRoot == null)
        {
            throw new InvalidOperationException(
                $"EnemyBehaviorTreeAsset '{name}': customRoot 为空。请在 Behavior Tree Editor 中手动配置并 Save。");
        }

        return new NativeBehaviorTreeRunner(customRoot.Build());
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        graphLayout ??= new EnemyBehaviorGraphLayout();
        if (customRoot == null)
            return;

        // 旧 Sequence 叶子 Condition → 装饰链（消除 child 为空）
        if (EnemyBehaviorTreeTopologyNormalizer.Normalize(customRoot))
            UnityEditor.EditorUtility.SetDirty(this);

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
