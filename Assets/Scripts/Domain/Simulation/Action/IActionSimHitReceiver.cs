/// <summary>接收帧末战斗结算产生的稳定动作实例命中确认。</summary>
public interface IActionSimHitReceiver
{
    /// <summary>仅当 actionInstanceId 与当前动作实例匹配时确认命中。</summary>
    bool ConfirmHit(int actionInstanceId);
}
