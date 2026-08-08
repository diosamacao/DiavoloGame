/// <summary>运行时 Hurtbox 线框调试开关；由 F4 / HUD 切换，供 Visualizer 读取。</summary>
public static class CombatHurtboxDebugSettings
{
    /// <summary>为 true 时绘制全部已注册目标的逻辑 Hurtbox。</summary>
    public static bool ShowHurtboxes { get; set; }
}
