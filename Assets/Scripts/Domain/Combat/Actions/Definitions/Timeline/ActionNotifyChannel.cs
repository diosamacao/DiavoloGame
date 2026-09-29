/// <summary>时间轴 Notify 通道：Gameplay 必须执行，Presentation 仅完整表现装配执行。</summary>
public enum ActionNotifyChannel : byte
{
    /// <summary>位移、Hitbox、Cancel、资源等玩法事件。</summary>
    Gameplay = 0,

    /// <summary>VFX / SFX / 镜头震动等表现事件。</summary>
    Presentation = 1,
}
