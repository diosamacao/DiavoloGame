using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>BD 风格节点：深色本体 + 类别色标题条 + 上下端口。</summary>
public sealed class EnemyBehaviorGraphNodeView : Node
{
    Label _categoryBadge;

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

        viewDataKey = Def.NodeGuid;
        AddToClassList("bt-node");
        AddToClassList(EnemyBehaviorTreeStyle.CategoryClass(Def));

        style.minWidth = 150f;
        style.backgroundColor = EnemyBehaviorTreeStyle.NodeBody;

        RefreshTitle();
        CreateCategoryBadge();
        CreatePorts();
        ApplyTitleBarColor();

        RefreshExpandedState();
        RefreshPorts();
    }

    /// <summary>刷新标题（Inspector 改名后）。</summary>
    public void RefreshTitle()
    {
        title = string.IsNullOrEmpty(Def.NodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(Def)
            : Def.NodeName;
        if (_categoryBadge != null)
            _categoryBadge.text = EnemyBehaviorTreeStyle.CategoryLabel(Def);
    }

    /// <summary>Play 调试高亮（对齐 BD Running 描边）。</summary>
    public void SetDebugHighlight(bool on)
    {
        EnableInClassList("bt-running", on);
        if (on)
        {
            style.borderLeftWidth = 0;
            style.borderTopWidth = 0;
            style.borderRightWidth = 0;
            style.borderBottomWidth = 0;
            // class 负责描边；再加一层保险色
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;
            style.borderTopColor = EnemyBehaviorTreeStyle.Running;
            style.borderBottomColor = EnemyBehaviorTreeStyle.Running;
            style.borderLeftColor = EnemyBehaviorTreeStyle.Running;
            style.borderRightColor = EnemyBehaviorTreeStyle.Running;
        }
        else
        {
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            var edge = new Color(0.08f, 0.08f, 0.08f, 1f);
            style.borderTopColor = edge;
            style.borderBottomColor = edge;
            style.borderLeftColor = edge;
            style.borderRightColor = edge;
        }
    }

    void CreateCategoryBadge()
    {
        _categoryBadge = new Label(EnemyBehaviorTreeStyle.CategoryLabel(Def));
        _categoryBadge.AddToClassList("bt-category-badge");
        // 放在标题下方，类似 BD 显示任务类别
        mainContainer.Insert(1, _categoryBadge);
    }

    void CreatePorts()
    {
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
        extensionContainer.Add(OutputPort);
        expanded = true;
    }

    void ApplyTitleBarColor()
    {
        Color tint = EnemyBehaviorTreeStyle.TitleColorFor(Def);
        titleContainer.style.backgroundColor = tint;
        var titleLabel = titleContainer.Q<Label>("title-label");
        if (titleLabel != null)
            titleLabel.style.color = Color.white;
    }
}
