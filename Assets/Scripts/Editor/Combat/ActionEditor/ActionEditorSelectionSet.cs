using System.Collections.Generic;

/// <summary>时间轴多选集合；Primary 供右侧 Inspector 编辑。</summary>
public sealed class ActionEditorSelectionSet
{
    readonly List<ActionEditorSelection> _items = new();

    /// <summary>主选中项；多选时 Inspector 编辑此项。</summary>
    public ActionEditorSelection Primary { get; private set; }

    /// <summary>当前选中数量。</summary>
    public int Count => _items.Count;

    /// <summary>是否有任意选中。</summary>
    public bool HasSelection => _items.Count > 0;

    /// <summary>全部选中项（只读顺序）。</summary>
    public IReadOnlyList<ActionEditorSelection> Items => _items;

    /// <summary>清空选中。</summary>
    public void Clear()
    {
        _items.Clear();
        Primary = default;
    }

    /// <summary>替换为单项选中。</summary>
    public void Set(ActionEditorSelection item)
    {
        _items.Clear();
        _items.Add(item);
        Primary = item;
    }

    /// <summary>用另一集合整体替换当前选中。</summary>
    public void ReplaceWith(ActionEditorSelectionSet other)
    {
        Clear();
        if (other == null || !other.HasSelection)
            return;

        for (int i = 0; i < other.Items.Count; i++)
            _items.Add(other.Items[i]);
        Primary = other.Primary;
    }

    /// <summary>Ctrl 多选：切换单项；若取消的是 Primary 则改选其余第一项。</summary>
    public void Toggle(ActionEditorSelection item)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].Equals(item))
                continue;

            _items.RemoveAt(i);
            if (Primary.Equals(item))
                Primary = _items.Count > 0 ? _items[0] : default;
            return;
        }

        _items.Add(item);
        Primary = item;
    }

    /// <summary>将多项并入选中（已存在则跳过）；Primary 设为 last。</summary>
    public void AddRange(IEnumerable<ActionEditorSelection> items, ActionEditorSelection last)
    {
        foreach (ActionEditorSelection item in items)
        {
            if (!Contains(item))
                _items.Add(item);
        }

        if (last.ArrayProperty != null)
            Primary = last;
        else if (_items.Count > 0 && Primary.ArrayProperty == null)
            Primary = _items[0];
    }

    /// <summary>是否包含指定窗口。</summary>
    public bool Contains(ActionEditorSelection item)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Equals(item))
                return true;
        }

        return false;
    }
}
