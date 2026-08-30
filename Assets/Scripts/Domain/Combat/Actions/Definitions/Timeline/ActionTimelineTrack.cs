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
    /// <summary>默认动画轨：展示 ActionDefinition.animationSegments，非 timeline 窗口数组。</summary>
    Animation = 9,
    /// <summary>完美闪避窗：玩家 Dodge 上窗内被命中时 Pipeline 吞伤并武装反击缓冲。</summary>
    PerfectDodgeWindow = 10,
    /// <summary>位移修正窗：SoftBodySuppress / TargetAdhesion（Wave 4）。</summary>
    MotionModifier = 11,
    /// <summary>离散位移点事件：Relocate 等（Wave 4，可选）。</summary>
    MotionCommand = 12,
    /// <summary>纯表现镜头区间；CameraShotPlayer 消费，ActionSim 不执行。</summary>
    Camera = 13,
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
