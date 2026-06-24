/// <summary>招式开始时的副作用执行上下文（由 PlayerController 等 Motor 层实现并注入）。</summary>
public interface IActionStartContext
{
    void FaceBufferedMoveIntent();
}
