using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>W8 启动覆盖：CLI &gt; Env &gt; File &gt; Default，以及非法值与密钥键。</summary>
public sealed class ServerLaunchConfigResolverTests
{
    static readonly ServerLaunchConfig Defaults = ServerLaunchConfig.CreateDefault(
        bindPort: 1,
        contentVersion: 1,
        maxPlayers: 2);

    /// <summary>无覆盖时保持默认绑定。</summary>
    [Test]
    public void NoOverlays_KeepsDefaults()
    {
        Assert.That(
            TryResolve(Defaults, null, null, null, out ServerLaunchConfig config, out ServerExitCode exit),
            Is.True);
        Assert.That(exit, Is.EqualTo(ServerExitCode.Success));
        Assert.That(config.BindPort, Is.EqualTo(1));
        Assert.That(config.MaxPlayers, Is.EqualTo(2));
        Assert.That(config.ExitOnMatchEnd, Is.False);
        Assert.That(config.EmptyLobbyTimeoutMs, Is.EqualTo(0));
    }

    /// <summary>文件覆盖默认，环境覆盖文件，命令行覆盖环境。</summary>
    [Test]
    public void Priority_CliBeatsEnvBeatsFileBeatsDefault()
    {
        const string fileText = "port=2\nmaxPlayers=3\nemptyLobbyTimeoutMs=10\n";
        var env = new Dictionary<string, string>
        {
            ["ACTGAME_PORT"] = "3",
            ["ACTGAME_MAX_PLAYERS"] = "4",
        };

        Assert.That(
            TryResolve(
                Defaults,
                name => env.TryGetValue(name, out string value) ? value : null,
                new[] { "exe", "-actgame-config", "ignored.cfg", "-actgame-port", "4" },
                path => path == "ignored.cfg" ? fileText : null,
                out ServerLaunchConfig config,
                out ServerExitCode exit),
            Is.True);
        Assert.That(exit, Is.EqualTo(ServerExitCode.Success));
        Assert.That(config.BindPort, Is.EqualTo(4));
        Assert.That(config.MaxPlayers, Is.EqualTo(4));
        Assert.That(config.EmptyLobbyTimeoutMs, Is.EqualTo(10));
    }

    /// <summary>仅文件时覆盖默认端口与退出策略；未指定路径时不读文件。</summary>
    [Test]
    public void File_OverridesDefaultLifetime()
    {
        bool fileReadWithoutPath = false;
        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                null,
                _ =>
                {
                    fileReadWithoutPath = true;
                    return "port=7777\n";
                },
                out ServerLaunchConfig ignored,
                out _),
            Is.True);
        Assert.That(fileReadWithoutPath, Is.False);
        Assert.That(ignored.BindPort, Is.EqualTo(1));

        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                new[] { "-actgame-config", "server.cfg" },
                path => path == "server.cfg" ? "port=7777\nexitOnMatchEnd=1\nempty-lobby-ms=120000\n" : null,
                out ServerLaunchConfig config,
                out ServerExitCode exit),
            Is.True);
        Assert.That(exit, Is.EqualTo(ServerExitCode.Success));
        Assert.That(config.BindPort, Is.EqualTo(7777));
        Assert.That(config.ExitOnMatchEnd, Is.True);
        Assert.That(config.EmptyLobbyTimeoutMs, Is.EqualTo(120000));
    }

    /// <summary>环境覆盖文件，且 CLI 配置路径优先于 ACTGAME_CONFIG。</summary>
    [Test]
    public void ConfigPath_CliBeatsEnv_AndEnvBeatsFilePort()
    {
        var env = new Dictionary<string, string>
        {
            [ServerLaunchConfigResolver.EnvConfigPath] = "env.cfg",
            ["ACTGAME_PORT"] = "9",
        };

        Assert.That(
            TryResolve(
                Defaults,
                name => env.TryGetValue(name, out string value) ? value : null,
                new[] { "-actgame-config", "cli.cfg" },
                path =>
                {
                    if (path == "cli.cfg")
                        return "port=5\n";
                    if (path == "env.cfg")
                        return "port=6\n";
                    return null;
                },
                out ServerLaunchConfig config,
                out _),
            Is.True);
        Assert.That(config.BindPort, Is.EqualTo(9));
    }

    /// <summary>指定了配置文件但读不到时为 ConfigFailed。</summary>
    [Test]
    public void MissingConfigFile_ReturnsConfigFailed()
    {
        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                new[] { "-actgame-config", "missing.cfg" },
                _ => null,
                out ServerLaunchConfig config,
                out ServerExitCode exit),
            Is.False);
        Assert.That(exit, Is.EqualTo(ServerExitCode.ConfigFailed));
        Assert.That(config.Validate(out _), Is.False);
    }

    /// <summary>非法端口或空房超时不得通过 Validate。</summary>
    [Test]
    public void InvalidOverlay_ReturnsConfigFailed()
    {
        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                new[] { "-actgame-port", "abc" },
                null,
                out _,
                out ServerExitCode parseExit),
            Is.False);
        Assert.That(parseExit, Is.EqualTo(ServerExitCode.ConfigFailed));

        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                new[] { "-actgame-empty-lobby-ms", "-1" },
                null,
                out ServerLaunchConfig config,
                out ServerExitCode timeoutExit),
            Is.False);
        Assert.That(timeoutExit, Is.EqualTo(ServerExitCode.ConfigFailed));
        Assert.That(config.Validate(out _), Is.False);
    }

    /// <summary>密钥键被忽略，且不会覆盖 bind。</summary>
    [Test]
    public void SecretKeys_AreIgnored()
    {
        Assert.That(
            TryResolve(
                Defaults,
                _ => null,
                new[] { "-actgame-config", "server.cfg" },
                _ => "bind=10.0.0.2\npassword=hunter2\ntoken=abc\n",
                out ServerLaunchConfig config,
                out ServerExitCode exit),
            Is.True);
        Assert.That(exit, Is.EqualTo(ServerExitCode.Success));
        Assert.That(config.BindHost, Is.EqualTo("10.0.0.2"));
    }

    static bool TryResolve(
        ServerLaunchConfig defaults,
        Func<string, string> getEnv,
        string[] args,
        Func<string, string> readFile,
        out ServerLaunchConfig config,
        out ServerExitCode exitCode) =>
        ServerLaunchConfigResolver.TryResolve(defaults, getEnv, args, readFile, out config, out exitCode);
}
