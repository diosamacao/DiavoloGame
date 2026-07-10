/// <summary>订阅统一 ActionNotify 时间轴事件的运行时消费者。</summary>
public interface IActionNotifyConsumer
{
    /// <summary>点事件触发时调用，例如转发自定义信号。</summary>
    void OnActionNotify(in ActionNotifyContext context);

    /// <summary>区间事件进入、持续或退出时调用；消费者根据 State 类型选择处理。</summary>
    void OnActionNotifyState(in ActionNotifyContext context);

    /// <summary>当前招式结束时调用；用于清理窗口内仍存活的实例。</summary>
    void OnActionEnded();
}
