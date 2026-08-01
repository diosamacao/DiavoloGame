/// <summary>招式模拟核消费的设备无关玩法意图缓冲。</summary>
public interface IActionInputBuffer
{
    /// <summary>返回指定玩法意图当前是否仍在有效缓冲期。</summary>
    bool HasBuffer(GameplayIntentType intent);

    /// <summary>尝试消费指定玩法意图，成功时将其从缓冲移除。</summary>
    bool TryConsumeBuffer(GameplayIntentType intent);
}
