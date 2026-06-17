/// <summary>招式运行时消费的输入缓冲（多槽位）。</summary>
public interface IActionComboInput
{
    bool HasBuffer(InputSlot slot);

    bool TryConsumeBuffer(InputSlot slot);
}
