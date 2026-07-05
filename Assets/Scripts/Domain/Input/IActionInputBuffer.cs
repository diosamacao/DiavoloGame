/// <summary>招式运行时消费的离散输入缓冲（多输入 id）。</summary>
public interface IActionInputBuffer
{
    bool HasBuffer(string inputId);

    bool TryConsumeBuffer(string inputId);
}
