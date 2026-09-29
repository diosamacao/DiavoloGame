using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>动作工作区唯一分栏布局；现有时间轴与属性各使用一个 IMGUI 画布。</summary>
public sealed class ActionEditorWorkspaceView
{
    readonly TwoPaneSplitView librarySplit;

    /// <summary>构建上下分区及资产/视口/属性布局，尺寸由 UI Toolkit 持久化。</summary>
    public ActionEditorWorkspaceView(VisualElement root, Action header, Action<Rect> library,
        Action<Rect> viewport, Action<Rect> inspector, Action<Rect> timeline)
    {
        root.Clear();
        root.style.backgroundColor = new Color(.105f, .12f, .15f);
        var bar = new IMGUIContainer(header);
        bar.style.height = 48;
        root.Add(bar);
        var vertical = new TwoPaneSplitView(0, 310, TwoPaneSplitViewOrientation.Vertical)
        { viewDataKey = "ActionWorkspace.Vertical." + Application.dataPath };
        vertical.style.flexGrow = 1;
        root.Add(vertical);
        librarySplit = new TwoPaneSplitView(0, 190, TwoPaneSplitViewOrientation.Horizontal)
        { viewDataKey = "ActionWorkspace.Library." + Application.dataPath };
        librarySplit.style.minHeight = 180;
        vertical.Add(librarySplit);
        librarySplit.Add(Canvas("动作库", library));
        var upper = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal)
        { viewDataKey = "ActionWorkspace.Inspector." + Application.dataPath };
        librarySplit.Add(upper);
        upper.Add(Canvas("预览", viewport));
        upper.Add(Canvas("属性", inspector));
        var tracks = Canvas("时间轴", timeline);
        tracks.style.minHeight = 180;
        vertical.Add(tracks);
    }

    /// <summary>折叠列表保留更大的预览面积，不改变动作选择或角色范围。</summary>
    public void SetLibraryVisible(bool visible)
    {
        if (visible) librarySplit.UnCollapse();
        else librarySplit.CollapseChild(0);
    }

    static VisualElement Canvas(string title, Action<Rect> draw)
    {
        var panel = new VisualElement();
        panel.style.flexGrow = 1;
        panel.style.minWidth = 100;
        panel.style.overflow = Overflow.Hidden;
        var label = new Label(title);
        label.style.height = 23;
        label.style.paddingLeft = 10;
        label.style.paddingTop = 3;
        label.style.color = new Color(.65f, .8f, .95f);
        label.style.backgroundColor = new Color(.16f, .19f, .24f);
        panel.Add(label);
        IMGUIContainer canvas = null;
        canvas = new IMGUIContainer(() => draw(new Rect(0, 0, canvas.contentRect.width, canvas.contentRect.height)));
        canvas.style.flexGrow = 1;
        canvas.focusable = true;
        panel.Add(canvas);
        return panel;
    }
}
