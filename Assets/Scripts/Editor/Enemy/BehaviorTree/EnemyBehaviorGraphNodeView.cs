using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UE 风格宿主节点：标题条 + 顶部装饰徽章栈（Condition/Decorator 不单独成图节点）。
/// </summary>
public sealed class EnemyBehaviorGraphNodeView : Node
{
    readonly List<EnemyBehaviorNodeDef> _decorators = new List<EnemyBehaviorNodeDef>();
    readonly Action<EnemyBehaviorGraphNodeView, EnemyBehaviorNodeDef> _onInspect;
    VisualElement _decoratorStack;
    Label _categoryBadge;
    EnemyBehaviorNodeDef _inspectedChip;

    /// <summary>宿主定义（Composite / Task）。</summary>
    public EnemyBehaviorNodeDef Def { get; }

    /// <summary>稳定 Guid（宿主）。</summary>
    public string NodeGuid => Def != null ? Def.NodeGuid : string.Empty;

    /// <summary>装饰栈（外→内，与运行时套娃一致）。</summary>
    public IReadOnlyList<EnemyBehaviorNodeDef> Decorators => _decorators;

    /// <summary>输入端口（顶部）。</summary>
    public Port InputPort { get; private set; }

    /// <summary>输出端口（底部）；叶 Task 为 null。</summary>
    public Port OutputPort { get; private set; }

    /// <summary>创建宿主节点视图。</summary>
    public EnemyBehaviorGraphNodeView(
        EnemyBehaviorNodeDef host,
        IReadOnlyList<EnemyBehaviorNodeDef> decoratorsOuterToInner,
        Action<EnemyBehaviorGraphNodeView, EnemyBehaviorNodeDef> onInspect)
    {
        Def = host ?? throw new ArgumentNullException(nameof(host));
        _onInspect = onInspect;
        if (string.IsNullOrEmpty(Def.NodeGuid))
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(Def);

        if (decoratorsOuterToInner != null)
        {
            for (int i = 0; i < decoratorsOuterToInner.Count; i++)
            {
                if (decoratorsOuterToInner[i] != null)
                    _decorators.Add(decoratorsOuterToInner[i]);
            }
        }

        viewDataKey = Def.NodeGuid;
        AddToClassList("bt-node");
        AddToClassList(EnemyBehaviorTreeStyle.CategoryClass(Def));
        style.minWidth = 170f;
        style.backgroundColor = EnemyBehaviorTreeStyle.NodeBody;

        CreateDecoratorStack();
        RefreshTitle();
        CreateCategoryBadge();
        CreatePorts();
        ApplyTitleBarColor();
        RefreshDecoratorChips();

        // 点节点本体 → 检视宿主
        RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            if (evt.target is VisualElement ve && IsUnderDecoratorStack(ve))
                return;
            InspectHost();
        });

        RefreshExpandedState();
        RefreshPorts();
    }

    /// <summary>在最外层插入装饰（UE：新装饰叠在顶部）。</summary>
    public void AddDecoratorOuter(EnemyBehaviorNodeDef decorator)
    {
        if (decorator == null || !EnemyBehaviorNodeCatalog.IsDecorator(decorator))
            return;
        if (string.IsNullOrEmpty(decorator.NodeGuid))
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(decorator);
        _decorators.Insert(0, decorator);
        RefreshDecoratorChips();
        InspectDecorator(decorator);
    }

    /// <summary>移除装饰徽章。</summary>
    public bool RemoveDecorator(EnemyBehaviorNodeDef decorator)
    {
        if (decorator == null)
            return false;
        bool removed = _decorators.Remove(decorator);
        if (removed)
        {
            if (_inspectedChip == decorator)
                InspectHost();
            RefreshDecoratorChips();
        }

        return removed;
    }

    /// <summary>刷新标题。</summary>
    public void RefreshTitle()
    {
        title = string.IsNullOrEmpty(Def.NodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(Def)
            : Def.NodeName;
        if (_categoryBadge != null)
            _categoryBadge.text = EnemyBehaviorTreeStyle.CategoryLabel(Def);
    }

    /// <summary>宿主或任一装饰名命中则高亮。</summary>
    public void SetDebugHighlight(HashSet<string> names)
    {
        bool on = names != null && names.Contains(title);
        if (!on && names != null)
        {
            for (int i = 0; i < _decorators.Count; i++)
            {
                string label = EnemyBehaviorGraphPresentation.ChipLabel(_decorators[i]);
                if (names.Contains(label))
                {
                    on = true;
                    break;
                }
            }
        }

        EnableInClassList("bt-running", on);
        Color edge = on ? EnemyBehaviorTreeStyle.Running : new Color(0.08f, 0.08f, 0.08f, 1f);
        int w = on ? 2 : 1;
        style.borderTopWidth = w;
        style.borderBottomWidth = w;
        style.borderLeftWidth = w;
        style.borderRightWidth = w;
        style.borderTopColor = edge;
        style.borderBottomColor = edge;
        style.borderLeftColor = edge;
        style.borderRightColor = edge;
    }

    /// <summary>标记当前检视的徽章（高亮 chip）。</summary>
    public void SetInspectedChip(EnemyBehaviorNodeDef decoratorOrNull)
    {
        _inspectedChip = decoratorOrNull;
        RefreshDecoratorChips();
    }

    void InspectHost()
    {
        _inspectedChip = null;
        RefreshDecoratorChips();
        _onInspect?.Invoke(this, Def);
    }

    void InspectDecorator(EnemyBehaviorNodeDef decorator)
    {
        _inspectedChip = decorator;
        RefreshDecoratorChips();
        _onInspect?.Invoke(this, decorator);
    }

    void CreateDecoratorStack()
    {
        _decoratorStack = new VisualElement();
        _decoratorStack.AddToClassList("bt-decorator-stack");
        // 插在节点最上方，模拟 UE 装饰贴在节点顶
        mainContainer.Insert(0, _decoratorStack);
    }

    void RefreshDecoratorChips()
    {
        _decoratorStack.Clear();
        for (int i = 0; i < _decorators.Count; i++)
        {
            EnemyBehaviorNodeDef dec = _decorators[i];
            var chip = new Button(() => InspectDecorator(dec))
            {
                text = EnemyBehaviorGraphPresentation.ChipLabel(dec),
                tooltip = "点击编辑参数；右键移除",
            };
            chip.AddToClassList("bt-decorator-chip");
            if (EnemyBehaviorNodeCatalog.IsCondition(dec))
                chip.AddToClassList("bt-decorator-chip-condition");
            else
                chip.AddToClassList("bt-decorator-chip-structural");
            if (_inspectedChip == dec)
                chip.AddToClassList("bt-decorator-chip-selected");

            chip.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Remove Decorator", _ =>
                {
                    RemoveDecorator(dec);
                    _onInspect?.Invoke(this, Def);
                });
            }));

            _decoratorStack.Add(chip);
        }

        _decoratorStack.style.display = _decorators.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void CreateCategoryBadge()
    {
        _categoryBadge = new Label(EnemyBehaviorTreeStyle.CategoryLabel(Def));
        _categoryBadge.AddToClassList("bt-category-badge");
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

    static bool IsUnderDecoratorStack(VisualElement ve)
    {
        while (ve != null)
        {
            if (ve.ClassListContains("bt-decorator-stack") || ve.ClassListContains("bt-decorator-chip"))
                return true;
            ve = ve.parent;
        }

        return false;
    }
}
