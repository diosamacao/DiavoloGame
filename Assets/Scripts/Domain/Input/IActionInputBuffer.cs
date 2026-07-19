/// <summary>招式运行时消费的设备无关玩法意图缓冲。</summary>
public interface IActionInputBuffer
{
    bool HasBuffer(GameplayIntentType intent);

    bool TryConsumeBuffer(GameplayIntentType intent);
}
