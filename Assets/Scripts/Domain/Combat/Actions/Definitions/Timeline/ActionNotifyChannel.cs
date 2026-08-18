/// <summary>时间轴 Notify 通道：Gameplay 必须执行，Presentation 仅完整表现装配执行。</summary>
public enum ActionNotifyChannel : byte
{
    /// <summary>位移、Hitbox、Cancel、资源等玩法事件。</summary>
    Gameplay = 0,

    /// <summary>VFX / SFX / 镜头震动等表现事件。</summary>
    Presentation = 1,
}

/// <summary>按 Notify 运行时类型分类；Headless 工厂据此不注册表现消费者。</summary>
public static class ActionNotifyClassification
{
    /// <summary>点事件分类；未知类型默认 Gameplay，避免漏掉玩法。</summary>
    public static ActionNotifyChannel Classify(ActionNotify notify)
    {
        if (notify is PlayVfxNotify || notify is PlaySfxNotify)
            return ActionNotifyChannel.Presentation;
        return ActionNotifyChannel.Gameplay;
    }

    /// <summary>区间事件均为玩法窗口（Hitbox / Movement / Cancel / Rotation）。</summary>
    public static ActionNotifyChannel Classify(ActionNotifyState state) =>
        ActionNotifyChannel.Gameplay;
}
