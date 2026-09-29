using System;
using System.Collections.Generic;

/// <summary>客户端 Session 状态。</summary>
public enum ClientSessionState : byte
{
    /// <summary>尚未启动。</summary>
    Stopped = 0,

    /// <summary>已发送 Join，等待服务端结果。</summary>
    Connecting = 1,

    /// <summary>Join 完成，可收发应用消息。</summary>
    Joined = 2,

    /// <summary>被拒绝、Kick、超时或主动结束。</summary>
    Ended = 3,
}
