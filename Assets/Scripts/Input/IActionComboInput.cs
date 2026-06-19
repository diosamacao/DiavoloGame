/// <summary>招式运行时消费的输入缓冲（多输入 id）。</summary>
public interface IActionComboInput
{
    bool HasBuffer(string inputId);

    bool TryConsumeBuffer(string inputId);
}
