/// <summary>Dedicated 进程退出码；0 为正常，非 0 表示配置或绑定失败。</summary>
public enum ServerExitCode
{
    /// <summary>运行中或正常停服。</summary>
    Success = 0,

    /// <summary>ServerLaunchConfig 非法，未尝试绑端口。</summary>
    ConfigFailed = 10,

    /// <summary>Transport 绑定监听端点失败。</summary>
    BindFailed = 20,
}
