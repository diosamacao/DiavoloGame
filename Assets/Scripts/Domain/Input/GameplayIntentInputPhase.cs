/// <summary>原始离散输入被解释为玩法意图的时机。</summary>
public enum GameplayIntentInputPhase
{
    /// <summary>按下瞬间。</summary>
    Pressed = 0,
    /// <summary>持续按住达到绑定阈值的首帧。</summary>
    HoldReached = 1,
    /// <summary>松开瞬间。</summary>
    Released = 2,
}
