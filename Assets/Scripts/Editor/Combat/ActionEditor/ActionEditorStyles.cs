using UnityEditor;
using UnityEngine;

/// <summary>Action Editor 布局、面板色与轨道颜色常量。</summary>
public static class ActionEditorStyles
{
    public const float TrackHeight = 28f;
    public const float TrackHeaderWidth = 120f;
    public const float RulerHeight = 22f;
    public const float EdgeHandleWidth = 8f;
    public const int DefaultWindowFrames = 5;

    public const float SplitterWidth = 4f;
    public const float PanelHeaderHeight = 22f;
    public const float MinLeftWidth = 160f;
    public const float MaxLeftWidth = 360f;
    public const float MinRightWidth = 220f;
    public const float MaxRightWidth = 420f;
    public const float MinCenterWidth = 280f;
    public const float DefaultLeftWidth = 220f;
    public const float DefaultRightWidth = 300f;

    public static readonly Color Background = new(0.16f, 0.16f, 0.18f, 1f);
    public static readonly Color PanelLeft = new(0.19f, 0.19f, 0.21f, 1f);
    public static readonly Color PanelCenter = new(0.15f, 0.15f, 0.17f, 1f);
    public static readonly Color PanelRight = new(0.19f, 0.19f, 0.21f, 1f);
    public static readonly Color PanelHeader = new(0.22f, 0.22f, 0.25f, 1f);
    public static readonly Color Splitter = new(0.08f, 0.08f, 0.09f, 1f);
    public static readonly Color EmptyStateBox = new(0.2f, 0.2f, 0.23f, 1f);
    public static readonly Color Ruler = new(0.28f, 0.28f, 0.32f, 1f);
    public static readonly Color Playhead = new(1f, 0.35f, 0.2f, 1f);
    /// <summary>时间轴拖拽框选半透明填充。</summary>
    public static readonly Color MarqueeFill = new(0.35f, 0.6f, 1f, 0.18f);
    /// <summary>时间轴拖拽框选边框。</summary>
    public static readonly Color MarqueeBorder = new(0.45f, 0.75f, 1f, 0.95f);

    /// <summary>选中时相对原色的加深倍率（&lt; 1 更暗）。</summary>
    const float SelectionDarken = 0.72f;

    /// <summary>窗口条块圆角半径（像素）。</summary>
    public const float WindowCornerRadius = 4f;

    /// <summary>窗口条块描边宽度（像素）。</summary>
    public const float WindowBorderWidth = 1.25f;

    /// <summary>点事件菱形相对轨高的尺寸倍率；保证缩放下仍可点选拖拽。</summary>
    public const float PointEventDiamondSizeFactor = 0.85f;

    /// <summary>点事件菱形最小边长（像素）。</summary>
    public const float PointEventDiamondMinSize = 14f;

    /// <summary>时间轴缩放下限（1 = 铺满可视宽度）。</summary>
    public const float TimelineZoomMin = 1f;

    /// <summary>时间轴缩放上限，便于精确拖帧。</summary>
    public const float TimelineZoomMax = 16f;

    /// <summary>绘制圆角填充 + 描边的窗口条块，便于相邻窗口区分。</summary>
    public static void DrawRoundedWindowClip(Rect rect, Color fill, bool selected)
    {
        if (rect.width < 1f || rect.height < 1f)
            return;

        float radius = Mathf.Min(WindowCornerRadius, rect.height * 0.5f, rect.width * 0.5f);
        var radii = new Vector4(radius, radius, radius, radius);

        // 外圈描边：选中用更亮描边，未选中用深色细边。
        Color border = selected
            ? new Color(1f, 1f, 1f, 0.92f)
            : new Color(0f, 0f, 0f, 0.55f);

        GUI.DrawTexture(
            rect,
            EditorGUIUtility.whiteTexture,
            ScaleMode.StretchToFill,
            true,
            0f,
            border,
            Vector4.zero,
            radii);

        float inset = WindowBorderWidth;
        Rect inner = new(
            rect.x + inset,
            rect.y + inset,
            Mathf.Max(0f, rect.width - inset * 2f),
            Mathf.Max(0f, rect.height - inset * 2f));

        if (inner.width < 1f || inner.height < 1f)
            return;

        float innerRadius = Mathf.Max(0f, radius - inset);
        var innerRadii = new Vector4(innerRadius, innerRadius, innerRadius, innerRadius);
        GUI.DrawTexture(
            inner,
            EditorGUIUtility.whiteTexture,
            ScaleMode.StretchToFill,
            true,
            0f,
            fill,
            Vector4.zero,
            innerRadii);
    }

    /// <summary>
    /// 按轨高计算点事件菱形外接矩形；以触发帧中心为锚点，不受 1 帧条宽限制。
    /// </summary>
    public static Rect GetPointEventDiamondRect(Rect laneRect, int startFrame, float pixelsPerFrame)
    {
        float size = Mathf.Max(PointEventDiamondMinSize, laneRect.height * PointEventDiamondSizeFactor);
        float centerX = laneRect.x + (startFrame + 0.5f) * pixelsPerFrame;
        float centerY = laneRect.y + laneRect.height * 0.5f;
        return new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size);
    }

    /// <summary>绘制单帧点事件菱形（VFX/SFX/Event），便于点击与拖拽触发帧。</summary>
    public static void DrawPointEventDiamond(Rect bounds, Color fill, bool selected)
    {
        if (bounds.width < 1f || bounds.height < 1f)
            return;

        Vector3 c = new(bounds.center.x, bounds.center.y, 0f);
        float hx = bounds.width * 0.5f;
        float hy = bounds.height * 0.5f;
        Vector3[] verts =
        {
            new(c.x, c.y - hy, 0f),
            new(c.x + hx, c.y, 0f),
            new(c.x, c.y + hy, 0f),
            new(c.x - hx, c.y, 0f),
        };

        Color border = selected
            ? new Color(1f, 1f, 1f, 0.95f)
            : new Color(0f, 0f, 0f, 0.65f);

        Handles.BeginGUI();
        Handles.color = fill;
        Handles.DrawAAConvexPolygon(verts);
        Handles.color = border;
        Handles.DrawAAPolyLine(2.25f, verts[0], verts[1], verts[2], verts[3], verts[0]);
        Handles.EndGUI();
    }

    /// <summary>绘制面板底色与顶栏标题。</summary>
    public static Rect DrawPanelChrome(Rect panelRect, string title, Color bodyColor)
    {
        EditorGUI.DrawRect(panelRect, bodyColor);

        Rect headerRect = new(panelRect.x, panelRect.y, panelRect.width, PanelHeaderHeight);
        EditorGUI.DrawRect(headerRect, PanelHeader);
        GUI.Label(
            new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 16f, headerRect.height),
            title,
            EditorStyles.boldLabel);

        // 标题底部分割线
        EditorGUI.DrawRect(
            new Rect(panelRect.x, headerRect.yMax - 1f, panelRect.width, 1f),
            Splitter);

        return new Rect(
            panelRect.x,
            headerRect.yMax,
            panelRect.width,
            Mathf.Max(0f, panelRect.height - PanelHeaderHeight));
    }

    /// <summary>绘制可拖拽竖向分割条热区。</summary>
    public static void DrawSplitter(Rect splitterRect)
    {
        EditorGUI.DrawRect(splitterRect, Splitter);
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
    }

    /// <summary>VFX / SFX / Event / MotionCommand 为单帧点事件轨，不可拉时长。</summary>
    public static bool IsPointEventTrack(ActionTimelineTrackKind kind) =>
        kind is ActionTimelineTrackKind.Vfx
            or ActionTimelineTrackKind.Sfx
            or ActionTimelineTrackKind.Event
            or ActionTimelineTrackKind.MotionCommand;

    /// <summary>按轨道类型返回窗口条块颜色。</summary>
    public static Color ColorForTrack(ActionTimelineTrackKind kind) => kind switch
    {
        ActionTimelineTrackKind.Hitbox => new Color(1f, 0.4f, 0.2f, 0.85f),
        ActionTimelineTrackKind.Hurtbox => new Color(0.35f, 0.7f, 1f, 0.85f),
        ActionTimelineTrackKind.Vfx => new Color(0.2f, 0.85f, 0.9f, 0.85f),
        ActionTimelineTrackKind.Sfx => new Color(0.9f, 0.35f, 0.75f, 0.85f),
        ActionTimelineTrackKind.Cancel => new Color(0.7f, 0.4f, 1f, 0.85f),
        ActionTimelineTrackKind.Movement => new Color(0.35f, 0.85f, 0.45f, 0.85f),
        ActionTimelineTrackKind.Rotation => new Color(0.75f, 0.85f, 0.3f, 0.85f),
        ActionTimelineTrackKind.Event => new Color(0.95f, 0.85f, 0.3f, 0.85f),
        ActionTimelineTrackKind.Phase => new Color(0.55f, 0.55f, 0.6f, 0.85f),
        ActionTimelineTrackKind.Animation => new Color(0.45f, 0.65f, 1f, 0.9f),
        ActionTimelineTrackKind.PerfectDodgeWindow => new Color(1f, 0.85f, 0.25f, 0.9f),
        ActionTimelineTrackKind.MotionModifier => new Color(0.25f, 0.9f, 0.65f, 0.9f),
        ActionTimelineTrackKind.MotionCommand => new Color(0.95f, 0.55f, 0.2f, 0.9f),
        _ => new Color(0.6f, 0.6f, 0.65f, 0.85f),
    };

    /// <summary>选中窗口颜色：在轨道原色上略微加深，并略提高不透明度。</summary>
    public static Color ColorForSelectedTrack(ActionTimelineTrackKind kind)
    {
        Color baseColor = ColorForTrack(kind);
        return new Color(
            baseColor.r * SelectionDarken,
            baseColor.g * SelectionDarken,
            baseColor.b * SelectionDarken,
            Mathf.Min(1f, baseColor.a + 0.1f));
    }

    /// <summary>轨道类型显示名。</summary>
    public static string DisplayName(ActionTimelineTrackKind kind) => kind switch
    {
        ActionTimelineTrackKind.Hitbox => "Hitbox",
        ActionTimelineTrackKind.Hurtbox => "Hurtbox",
        ActionTimelineTrackKind.Vfx => "VFX",
        ActionTimelineTrackKind.Sfx => "SFX",
        ActionTimelineTrackKind.Cancel => "Cancel",
        ActionTimelineTrackKind.Movement => "Movement",
        ActionTimelineTrackKind.Rotation => "Rotation",
        ActionTimelineTrackKind.Event => "Event",
        ActionTimelineTrackKind.Phase => "Phase",
        ActionTimelineTrackKind.Animation => "Animation",
        ActionTimelineTrackKind.PerfectDodgeWindow => "PerfectDodge",
        ActionTimelineTrackKind.MotionModifier => "MotionModifier",
        ActionTimelineTrackKind.MotionCommand => "MotionCommand",
        _ => kind.ToString(),
    };
}
