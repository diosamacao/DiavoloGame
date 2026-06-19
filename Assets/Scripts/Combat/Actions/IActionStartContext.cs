/// <summary>招式开始时的副作用执行上下文（由 PlayerController 等注入）。</summary>
public interface IActionStartContext
{
    void FaceBufferedMoveIntent();
}
