using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Behavior Designer 风格配色（自研近似，不复制 BD 资产）。
/// 深色画布 + 按类别着色标题条。
/// </summary>
public static class EnemyBehaviorTreeStyle
{
    public const string UssPath =
        "Assets/Scripts/Editor/Enemy/BehaviorTree/Styles/EnemyBehaviorTree.uss";

    /// <summary>画布背景。</summary>
    public static readonly Color CanvasBg = new Color(0.18f, 0.18f, 0.18f, 1f);

    /// <summary>节点本体底色。</summary>
    public static readonly Color NodeBody = new Color(0.27f, 0.27f, 0.27f, 1f);

    /// <summary>选中描边。</summary>
    public static readonly Color Selection = new Color(0.95f, 0.78f, 0.22f, 1f);

    /// <summary>Running 高亮（偏 BD 运行黄绿）。</summary>
    public static readonly Color Running = new Color(0.45f, 0.85f, 0.35f, 1f);

    /// <summary>Composite 标题条（蓝）。</summary>
    public static readonly Color Composite = new Color(0.22f, 0.42f, 0.62f, 1f);

    /// <summary>Decorator 标题条（紫灰）。</summary>
    public static readonly Color Decorator = new Color(0.42f, 0.32f, 0.52f, 1f);

    /// <summary>Condition 标题条（绿）。</summary>
    public static readonly Color Condition = new Color(0.25f, 0.48f, 0.28f, 1f);

    /// <summary>Task/Action 标题条（橙）。</summary>
    public static readonly Color Task = new Color(0.62f, 0.42f, 0.18f, 1f);

    /// <summary>工具栏 / 侧栏底。</summary>
    public static readonly Color PanelBg = new Color(0.16f, 0.16f, 0.16f, 1f);

    /// <summary>侧栏分割线。</summary>
    public static readonly Color PanelBorder = new Color(0.08f, 0.08f, 0.08f, 1f);

    /// <summary>按 Def 取类别标题色。</summary>
    public static Color TitleColorFor(EnemyBehaviorNodeDef def)
    {
        if (def == null)
            return NodeBody;
        if (EnemyBehaviorNodeCatalog.IsComposite(def))
            return Composite;
        if (EnemyBehaviorNodeCatalog.IsDecorator(def))
            return Decorator;
        if (def.GetType().Name.IndexOf("Condition", System.StringComparison.Ordinal) >= 0)
            return Condition;
        return Task;
    }

    /// <summary>类别显示名（侧栏图例 / 节点徽标）。</summary>
    public static string CategoryLabel(EnemyBehaviorNodeDef def)
    {
        if (def == null)
            return "Node";
        if (EnemyBehaviorNodeCatalog.IsComposite(def))
            return "Composite";
        if (EnemyBehaviorNodeCatalog.IsDecorator(def))
            return "Decorator";
        if (def.GetType().Name.IndexOf("Condition", System.StringComparison.Ordinal) >= 0)
            return "Conditional";
        return "Action";
    }

    /// <summary>USS 类别 class。</summary>
    public static string CategoryClass(EnemyBehaviorNodeDef def)
    {
        if (def == null)
            return "bt-task";
        if (EnemyBehaviorNodeCatalog.IsComposite(def))
            return "bt-composite";
        if (EnemyBehaviorNodeCatalog.IsDecorator(def))
            return "bt-decorator";
        if (def.GetType().Name.IndexOf("Condition", System.StringComparison.Ordinal) >= 0)
            return "bt-condition";
        return "bt-task";
    }

    /// <summary>尝试加载并附加样式表。</summary>
    public static void TryApplyStyleSheet(VisualElement root)
    {
        if (root == null)
            return;
#if UNITY_EDITOR
        var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if (sheet != null && !root.styleSheets.Contains(sheet))
            root.styleSheets.Add(sheet);
#endif
    }
}
