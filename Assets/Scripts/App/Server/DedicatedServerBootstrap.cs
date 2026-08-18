using System;
using UnityEngine;

/// <summary>Dedicated 进程 Unity 入口：只装配 ServerRuntime，不创建玩家、输入、相机或 HUD。</summary>
[DefaultExecutionOrder(-210)]
[DisallowMultipleComponent]
public sealed class DedicatedServerBootstrap : MonoBehaviour
{
    DedicatedServerRuntime _runtime;
    ServerLaunchConfig _config;
    bool _configured;

    /// <summary>当前运行时；配置失败时为 null。</summary>
    public DedicatedServerRuntime Runtime => _runtime;

    /// <summary>最近一次启动退出码。</summary>
    public ServerExitCode ExitCode { get; private set; } = ServerExitCode.ConfigFailed;

    /// <summary>由 Composition Root 注入启动配置；重复调用会先释放旧运行时。</summary>
    public void Configure(ServerLaunchConfig config)
    {
        ShutdownRuntime();
        _config = config;
        _configured = true;
        if (!isActiveAndEnabled)
            return;
        StartRuntime();
    }

    void OnEnable()
    {
        if (_configured && _runtime == null)
            StartRuntime();
    }

    /// <summary>只泵 Session / Match / 每连接 ACK；权威 World 步进属 W6。</summary>
    void Update()
    {
        if (_runtime == null)
            return;
        _runtime.Poll(NowMs());
    }

    void OnDisable() => ShutdownRuntime();

    void OnDestroy() => ShutdownRuntime();

    /// <summary>创建 UDP Session；绑定失败写退出码，玩家构建下退出进程。</summary>
    void StartRuntime()
    {
        if (_runtime != null)
            return;

        _runtime = DedicatedServerRuntime.TryStart(new UdpTransport(), _config, out ServerExitCode exitCode);
        ExitCode = exitCode;
        if (_runtime != null)
        {
            Debug.Log($"DedicatedServerBootstrap: Listening {_runtime.Session.LocalEndpoint}。", this);
            return;
        }

        Debug.LogError($"DedicatedServerBootstrap: 启动失败 exit={exitCode}。", this);
#if !UNITY_EDITOR
        Application.Quit((int)exitCode);
#endif
    }

    void ShutdownRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
