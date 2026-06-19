/// <summary>离散输入标识；与 Input System Action 名及 CancelWindow.allowedInputs 对齐。</summary>
public static class InputIds
{
    public const string Attack = "Attack";
    public const string Dodge = "Dodge";
    /// <summary>连续移动意图在取消窗口中的占位 id（由 PlayerController 用 HasMoveIntent 判定）。</summary>
    public const string Move = "Move";
}
