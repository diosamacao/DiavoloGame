/// <summary>重定位失败回退。</summary>
public enum MotionFallbackPolicy
{
    CancelCommand = 0,
    CancelAction = 1,
    UseForwardOffset = 2,
}
