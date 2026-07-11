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

    /// <summary>选中时相对原色的加深倍率（&lt; 1 更暗）。</summary>
    const float SelectionDarken = 0.72f;

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
        _ => kind.ToString(),
    };
}
