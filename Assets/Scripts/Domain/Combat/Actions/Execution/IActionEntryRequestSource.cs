/// <summary>角色招式 Entry 请求只读源；CharacterActionDriver 不感知请求生产者身份。</summary>
public interface IActionEntryRequestSource
{
    /// <summary>取出并消费本帧 Entry 请求。</summary>
    bool TryConsume(out ActionEntryRequest request);
}
