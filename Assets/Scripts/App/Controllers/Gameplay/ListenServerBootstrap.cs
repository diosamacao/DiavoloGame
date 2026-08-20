using System;
using UnityEngine;

/// <summary>Listen 组合入口：同一 DedicatedServerRuntime + 本机 LocalClient（127.0.0.1 UDP）。</summary>
[DefaultExecutionOrder(-210)]
[DisallowMultipleComponent]
public sealed class ListenServerBootstrap : AppControllerBase
{
    DedicatedServerRuntime _server;
    LocalClientRuntime _local;
    ServerLaunchConfig _config;
    IDedicatedAuthorityWorld _authority;
    CombatWorldController _world;
    bool _configured;

    /// <summary>权威运行时；绑定失败时为 null。</summary>
    public DedicatedServerRuntime Server => _server;

    /// <summary>本机 Client；尚未 Start 或连接失败时为 null。</summary>
    public LocalClientRuntime LocalClient => _local;

    /// <summary>由 Composition Root 注入配置、权威世界与战斗入口；重复调用会先释放旧组合。</summary>
    public void Configure(
        ServerLaunchConfig config,
        IDedicatedAuthorityWorld authority,
        CombatWorldController world)
    {
        Shutdown();
        _config = config;
        _authority = authority;
        _world = world;
        _configured = true;
        if (isActiveAndEnabled)
            StartServer();
    }

    void OnEnable()
    {
        if (_configured && _server == null)
            StartServer();
    }

    void Start()
    {
        if (_server != null && _local == null)
            StartLocalClient();
    }

    /// <summary>
    /// 每渲染帧只采样边沿；按即将发生的权威步数发命令并预测。
    /// 禁止每个渲染帧 StepPrediction，否则动作加速、快照把人拉回。
    /// </summary>
    void Update()
    {
        if (_server == null)
            return;

        long nowMs = NowMs();
        _local?.PollAndApply(nowMs);
        _local?.SampleRenderInput();
        int steps = _server.PeekAdvanceSteps(nowMs);
        for (int i = 0; i < steps; i++)
            _local?.SendCommandAndPredict();
        _server.Poll(nowMs);
        _local?.PollAndApply(nowMs);
    }

    void LateUpdate() => _local?.Render();

    void OnDisable() => Shutdown();

    void OnDestroy() => Shutdown();

    /// <summary>启动与 Dedicated 相同的 ServerRuntime；端口占用时回退到系统分配端口。</summary>
    void StartServer()
    {
        if (_server != null)
            return;

        _server = DedicatedServerRuntime.TryStart(
            new UdpTransport(),
            _config,
            _authority,
            out ServerExitCode exitCode);
        if (_server == null && exitCode == ServerExitCode.BindFailed && _config.BindPort != 0)
        {
            _server = DedicatedServerRuntime.TryStart(
                new UdpTransport(),
                _config.WithBindPort(0),
                _authority,
                out exitCode);
        }

        if (_server != null && _server.IsReady)
        {
            int port = BoundPort();
            Debug.Log($"ListenServerBootstrap: READY port={port} role=ListenHost。", this);
            return;
        }

        Debug.LogError($"ListenServerBootstrap: 启动失败 exit={exitCode}。", this);
    }

    /// <summary>本机 Client 连 127.0.0.1:实际绑定端口；与远端客机同一 Command / Snapshot / ACK。</summary>
    void StartLocalClient()
    {
        if (_server == null || _local != null || _world == null)
            return;

        int port = BoundPort();
        if (port <= 0)
        {
            Debug.LogError("ListenServerBootstrap: 无法解析本机回环端口。", this);
            return;
        }

        var transport = new UdpTransport();
        ClientSession session = null;
        try
        {
            session = new ClientSession(transport, _server.Config.CreateSessionConfig());
            session.Start(new NetEndpoint("127.0.0.1", port), NowMs());
            _local = new LocalClientRuntime(
                _world,
                session,
                GetArchitecture(),
                transform,
                ReplicationRole.ListenHost);
            Debug.Log($"ListenServerBootstrap: 本机 Client 已请求加入 127.0.0.1:{port}。", this);
        }
        catch (Exception ex)
        {
            if (session != null)
                session.Dispose();
            else
                transport.Dispose();
            Debug.LogError($"ListenServerBootstrap: 本机 Client 连接失败。{ex.Message}", this);
        }
    }

    int BoundPort()
    {
        NetEndpoint? endpoint = _server?.Session?.LocalEndpoint;
        return endpoint.HasValue ? endpoint.Value.Port : _config.BindPort;
    }

    void Shutdown()
    {
        _local?.Dispose();
        _local = null;
        _server?.Dispose();
        _server = null;
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
