using UnityEngine;

/// <summary>Action Editor 布局与轨道颜色常量。</summary>
public static class ActionEditorStyles
{
    public const float TrackHeight = 28f;
    public const float TrackHeaderWidth = 120f;
    public const float RulerHeight = 22f;
    public const float EdgeHandleWidth = 8f;
    public const int DefaultWindowFrames = 5;

    public static readonly Color Background = new(0.16f, 0.16f, 0.18f, 1f);
    public static readonly Color Ruler = new(0.28f, 0.28f, 0.32f, 1f);
    public static readonly Color Playhead = new(1f, 0.35f, 0.2f, 1f);

    /// <summary>选中时相对原色的加深倍率（&lt; 1 更暗）。</summary>
    const float SelectionDarken = 0.72f;

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
