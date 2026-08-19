using System;
using UnityEngine;

/// <summary>Dedicated 进程 Unity 入口：只装配 ServerRuntime，不创建玩家、输入、相机或 HUD。</summary>
[DefaultExecutionOrder(-210)]
[DisallowMultipleComponent]
public sealed class DedicatedServerBootstrap : MonoBehaviour
{
    DedicatedServerRuntime _runtime;
    ServerLaunchConfig _config;
    IDedicatedAuthorityWorld _authority;
    bool _configured;

    /// <summary>当前运行时；配置失败时为 null。</summary>
    public DedicatedServerRuntime Runtime => _runtime;

    /// <summary>最近一次启动退出码。</summary>
    public ServerExitCode ExitCode { get; private set; } = ServerExitCode.ConfigFailed;

    /// <summary>由 Composition Root 注入启动配置与权威世界；重复调用会先释放旧运行时。</summary>
    public void Configure(ServerLaunchConfig config, IDedicatedAuthorityWorld authority)
    {
        ShutdownRuntime();
        _config = config;
        _authority = authority;
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

    /// <summary>泵 Session / Match / 命令；玩家构建在 ShouldExit 后以退出码停进程，Editor 只释放运行时。</summary>
    void Update()
    {
        if (_runtime == null)
            return;
        _runtime.Poll(NowMs());
        if (!_runtime.ShouldExit)
            return;

        ExitCode = _runtime.ExitCode;
        Debug.Log($"DedicatedServerBootstrap: 退出 exit={(int)ExitCode}。", this);
#if !UNITY_EDITOR
        Application.Quit((int)ExitCode);
#else
        ShutdownRuntime();
#endif
    }

    void OnDisable() => ShutdownRuntime();

    void OnDestroy() => ShutdownRuntime();

    /// <summary>创建 UDP Session；绑定失败写退出码，玩家构建下退出进程。</summary>
    void StartRuntime()
    {
        if (_runtime != null)
            return;

        _runtime = DedicatedServerRuntime.TryStart(
            new UdpTransport(),
            _config,
            _authority,
            out ServerExitCode exitCode);
        ExitCode = exitCode;
        if (_runtime != null && _runtime.IsReady)
        {
            NetEndpoint? endpoint = _runtime.Session.LocalEndpoint;
            int port = endpoint.HasValue ? endpoint.Value.Port : _config.BindPort;
            Debug.Log($"DedicatedServerBootstrap: READY port={port} role=DedicatedServer。", this);
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
