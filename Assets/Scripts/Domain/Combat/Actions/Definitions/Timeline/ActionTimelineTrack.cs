using System;

/// <summary>时间轴轨道类型；编辑器手动加轨与窗口类型约束共用。</summary>
public enum ActionTimelineTrackKind
{
    Hitbox = 0,
    Hurtbox = 1,
    Vfx = 2,
    Sfx = 3,
    Cancel = 4,
    Movement = 5,
    Rotation = 6,
    Event = 7,
    Phase = 8,
}

/// <summary>时间轴轨道描述；允许空轨存在，窗口通过 trackName 归属到轨。</summary>
[Serializable]
public class ActionTimelineTrack
{
    [UnityEngine.SerializeField] string trackName = "Track";
    [UnityEngine.SerializeField] ActionTimelineTrackKind kind = ActionTimelineTrackKind.Hitbox;
    [UnityEngine.SerializeField] bool visible = true;

    /// <summary>轨道显示名，同时作为窗口 trackName 归属键。</summary>
    public string TrackName => string.IsNullOrEmpty(trackName) ? "Track" : trackName;

    /// <summary>轨道类型，约束可添加的窗口种类。</summary>
    public ActionTimelineTrackKind Kind => kind;

    /// <summary>编辑器是否绘制该轨。</summary>
    public bool Visible => visible;
}
