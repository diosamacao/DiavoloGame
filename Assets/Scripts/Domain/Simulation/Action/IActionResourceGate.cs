/// <summary>动作起手/切招资源鉴权与扣费；实现放在 Domain，ActionSim 只依赖本接口。</summary>
public interface IActionResourceGate
{
    /// <summary>当前是否负担得起该动作价签。</summary>
    bool CanAfford(IActionSimContent content);

    /// <summary>起手成功后扣费；仅在 Begin 成功路径调用一次。</summary>
    void CommitCost(IActionSimContent content);
}
