/// <summary>动作输入触发类型；当前仅落地 Pressed，Held / Released 待输入生命周期重构后接入。</summary>
public enum ActionInputTrigger
{
    /// <summary>按下瞬间触发（对应 InputReader 的 WasPressedThisFrame）。</summary>
    Pressed = 0,
}
