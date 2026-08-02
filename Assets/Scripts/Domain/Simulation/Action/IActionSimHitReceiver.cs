/// <summary>接收帧末战斗结算产生的稳定动作实例命中确认与逻辑卡肉。</summary>
public interface IActionSimHitReceiver
{
    /// <summary>仅当 actionInstanceId 与当前动作实例匹配时确认命中。</summary>
    bool ConfirmHit(int actionInstanceId);

    /// <summary>对匹配的当前实例施加逻辑 freezeFrames；oncePerAction 时同实例只接受首次。</summary>
    bool RequestHitStop(int actionInstanceId, int frames, bool oncePerAction);
}
