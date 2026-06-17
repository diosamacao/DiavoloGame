public interface IActionComboInput
{
    bool HasBufferedAttack { get; }

    void ConsumeBufferedAttack();
}
