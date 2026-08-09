using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>行为树 Graph 节点视图；上下端口（UE 风格：父上子下）。</summary>
public sealed class EnemyBehaviorGraphNodeView : Node
{
    /// <summary>对应的节点定义。</summary>
    public EnemyBehaviorNodeDef Def { get; }

    /// <summary>稳定 Guid。</summary>
    public string NodeGuid => Def != null ? Def.NodeGuid : string.Empty;

    /// <summary>输入端口（顶部，接父节点）。</summary>
    public Port InputPort { get; private set; }

    /// <summary>输出端口（底部）；叶节点为 null。</summary>
    public Port OutputPort { get; private set; }

    /// <summary>创建节点视图。</summary>
    public EnemyBehaviorGraphNodeView(EnemyBehaviorNodeDef def)
    {
        Def = def ?? throw new System.ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(Def.NodeGuid))
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(Def);

        title = string.IsNullOrEmpty(Def.NodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(Def)
            : Def.NodeName;
        viewDataKey = Def.NodeGuid;
        style.minWidth = 140f;

        ApplyCategoryStyle();
        CreatePorts();
        RefreshExpandedState();
        RefreshPorts();
    }

    /// <summary>刷新标题（Inspector 改名后）。</summary>
    public void RefreshTitle()
    {
        title = string.IsNullOrEmpty(Def.NodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(Def)
            : Def.NodeName;
    }

    /// <summary>Play 调试高亮开关。</summary>
    public void SetDebugHighlight(bool on)
    {
        style.borderTopWidth = on ? 3 : 0;
        style.borderTopColor = on ? new Color(1f, 0.85f, 0.2f) : Color.clear;
    }

    void CreatePorts()
    {
        // UE 风格：In 在上、Out 在下（Vertical 端口）
        InputPort = InstantiatePort(
            Orientation.Vertical,
            Direction.Input,
            Port.Capacity.Single,
            typeof(bool));
        InputPort.portName = string.Empty;
        InputPort.style.alignSelf = Align.Center;
        titleContainer.Insert(0, InputPort);

        if (!EnemyBehaviorNodeCatalog.HasOutput(Def))
            return;

        Port.Capacity capacity = EnemyBehaviorNodeCatalog.IsComposite(Def)
            ? Port.Capacity.Multi
            : Port.Capacity.Single;
        OutputPort = InstantiatePort(
            Orientation.Vertical,
            Direction.Output,
            capacity,
            typeof(bool));
        OutputPort.portName = string.Empty;
        OutputPort.style.alignSelf = Align.Center;
        // extensionContainer 在节点底部；需保持展开才可见
        extensionContainer.Add(OutputPort);
        expanded = true;
    }

    void ApplyCategoryStyle()
    {
        Color tint;
        if (EnemyBehaviorNodeCatalog.IsComposite(Def))
            tint = new Color(0.25f, 0.45f, 0.7f);
        else if (EnemyBehaviorNodeCatalog.IsDecorator(Def))
            tint = new Color(0.55f, 0.35f, 0.65f);
        else if (Def.GetType().Name.Contains("Condition"))
            tint = new Color(0.3f, 0.55f, 0.35f);
        else
            tint = new Color(0.6f, 0.4f, 0.25f);

        titleContainer.style.backgroundColor = tint;
    }
}
