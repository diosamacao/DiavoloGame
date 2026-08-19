using System;
using System.Collections.Generic;

/// <summary>解析 Dedicated 启动覆盖；优先级 CLI &gt; Env &gt; File &gt; Default。不写日志，密钥键只忽略不回显。</summary>
public static class ServerLaunchConfigResolver
{
    /// <summary>指向 key=value 配置文件的环境变量名。</summary>
    public const string EnvConfigPath = "ACTGAME_CONFIG";

    /// <summary>指向配置文件的命令行开关（写成 -actgame-config）。</summary>
    public const string CliConfigFlag = "actgame-config";

    /// <summary>CLI 优先，其次环境变量；都未指定则返回空。</summary>
    public static string FindConfigPath(Func<string, string> getEnv, IReadOnlyList<string> args)
    {
        if (TryReadNamedArg(args, CliConfigFlag, out string cliPath) && !string.IsNullOrWhiteSpace(cliPath))
            return cliPath.Trim();

        string envPath = getEnv != null ? getEnv(EnvConfigPath) : null;
        return string.IsNullOrWhiteSpace(envPath) ? string.Empty : envPath.Trim();
    }

    /// <summary>把 File / Env / CLI 叠到默认值上；指定了配置文件却读不到则失败。密钥类键不会写入结果。</summary>
    public static bool TryResolve(
        ServerLaunchConfig defaults,
        Func<string, string> getEnv,
        IReadOnlyList<string> args,
        Func<string, string> readFile,
        out ServerLaunchConfig config,
        out ServerExitCode exitCode)
    {
        config = defaults;
        exitCode = ServerExitCode.Success;

        var overlays = new Dictionary<string, string>(StringComparer.Ordinal);
        string path = FindConfigPath(getEnv, args);
        if (!string.IsNullOrEmpty(path))
        {
            string fileText = readFile != null ? readFile(path) : null;
            if (fileText == null)
            {
                config = CreateInvalidSentinel();
                exitCode = ServerExitCode.ConfigFailed;
                return false;
            }

            if (!TryApplyFileText(fileText, overlays, out exitCode))
            {
                config = CreateInvalidSentinel();
                return false;
            }
        }

        ApplyEnv(getEnv, overlays);
        ApplyArgs(args, overlays);

        if (!TryBuild(defaults, overlays, out config, out exitCode))
            return false;

        return config.Validate(out exitCode);
    }

    /// <summary>解析失败时交给 TryStart，保证玩家构建以 ConfigFailed 退出。</summary>
    public static ServerLaunchConfig CreateInvalidSentinel() =>
        new(
            string.Empty,
            -1,
            contentVersion: 1,
            maxPlayers: 1,
            idleTimeoutMs: 10000,
            heartbeatIntervalMs: 500,
            new NetworkProtocolVersion(1),
            default);

    static bool TryApplyFileText(
        string fileText,
        Dictionary<string, string> overlays,
        out ServerExitCode exitCode)
    {
        exitCode = ServerExitCode.Success;
        if (string.IsNullOrEmpty(fileText))
            return true;

        string[] lines = fileText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            int split = line.IndexOf('=');
            if (split <= 0)
            {
                exitCode = ServerExitCode.ConfigFailed;
                return false;
            }

            string key = NormalizeKey(line.Substring(0, split));
            string value = line.Substring(split + 1).Trim();
            if (key.Length == 0 || IsSecretKey(key))
                continue;

            overlays[key] = value;
        }

        return true;
    }

    static void ApplyEnv(Func<string, string> getEnv, Dictionary<string, string> overlays)
    {
        if (getEnv == null)
            return;

        TryOverlayEnv(getEnv, overlays, "ACTGAME_BIND", "bind");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_PORT", "port");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_MAX_PLAYERS", "maxplayers");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_CONTENT_VERSION", "contentversion");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_IDLE_TIMEOUT_MS", "idletimeoutms");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_HEARTBEAT_MS", "heartbeatintervalms");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_EMPTY_LOBBY_MS", "emptylobbytimeoutms");
        TryOverlayEnv(getEnv, overlays, "ACTGAME_EXIT_ON_MATCH_END", "exitonmatchend");
    }

    static void TryOverlayEnv(
        Func<string, string> getEnv,
        Dictionary<string, string> overlays,
        string envName,
        string canonicalKey)
    {
        string value = getEnv(envName);
        if (!string.IsNullOrWhiteSpace(value) && !IsSecretKey(canonicalKey))
            overlays[canonicalKey] = value.Trim();
    }

    static void ApplyArgs(IReadOnlyList<string> args, Dictionary<string, string> overlays)
    {
        TryOverlayArg(args, overlays, "actgame-bind", "bind");
        TryOverlayArg(args, overlays, "actgame-port", "port");
        TryOverlayArg(args, overlays, "actgame-max-players", "maxplayers");
        TryOverlayArg(args, overlays, "actgame-content-version", "contentversion");
        TryOverlayArg(args, overlays, "actgame-idle-timeout-ms", "idletimeoutms");
        TryOverlayArg(args, overlays, "actgame-heartbeat-ms", "heartbeatintervalms");
        TryOverlayArg(args, overlays, "actgame-empty-lobby-ms", "emptylobbytimeoutms");
        TryOverlayArg(args, overlays, "actgame-exit-on-match-end", "exitonmatchend");
    }

    static void TryOverlayArg(
        IReadOnlyList<string> args,
        Dictionary<string, string> overlays,
        string flagName,
        string canonicalKey)
    {
        if (TryReadNamedArg(args, flagName, out string value) && !IsSecretKey(canonicalKey))
            overlays[canonicalKey] = value.Trim();
    }

    static bool TryBuild(
        ServerLaunchConfig defaults,
        Dictionary<string, string> overlays,
        out ServerLaunchConfig config,
        out ServerExitCode exitCode)
    {
        exitCode = ServerExitCode.Success;
        string bindHost = defaults.BindHost;
        int bindPort = defaults.BindPort;
        int contentVersion = defaults.ContentVersion;
        int maxPlayers = defaults.MaxPlayers;
        int idleTimeoutMs = defaults.IdleTimeoutMs;
        int heartbeatIntervalMs = defaults.HeartbeatIntervalMs;
        int emptyLobbyTimeoutMs = defaults.EmptyLobbyTimeoutMs;
        bool exitOnMatchEnd = defaults.ExitOnMatchEnd;

        if (!TryReadString(overlays, "bind", ref bindHost)
            || !TryReadString(overlays, "bindhost", ref bindHost)
            || !TryReadInt(overlays, "port", ref bindPort)
            || !TryReadInt(overlays, "bindport", ref bindPort)
            || !TryReadInt(overlays, "contentversion", ref contentVersion)
            || !TryReadInt(overlays, "maxplayers", ref maxPlayers)
            || !TryReadInt(overlays, "idletimeoutms", ref idleTimeoutMs)
            || !TryReadInt(overlays, "heartbeatintervalms", ref heartbeatIntervalMs)
            || !TryReadInt(overlays, "heartbeatms", ref heartbeatIntervalMs)
            || !TryReadInt(overlays, "emptylobbytimeoutms", ref emptyLobbyTimeoutMs)
            || !TryReadInt(overlays, "emptylobbyms", ref emptyLobbyTimeoutMs)
            || !TryReadBool(overlays, "exitonmatchend", ref exitOnMatchEnd))
        {
            config = CreateInvalidSentinel();
            exitCode = ServerExitCode.ConfigFailed;
            return false;
        }

        config = new ServerLaunchConfig(
            bindHost,
            bindPort,
            contentVersion,
            maxPlayers,
            idleTimeoutMs,
            heartbeatIntervalMs,
            defaults.ProtocolVersion,
            defaults.PlayerArchetypeId,
            defaults.GameplayFingerprint,
            emptyLobbyTimeoutMs,
            exitOnMatchEnd);
        return true;
    }

    static bool TryReadString(Dictionary<string, string> overlays, string key, ref string value)
    {
        if (!overlays.TryGetValue(key, out string raw) || raw == null)
            return true;
        value = raw.Trim();
        return true;
    }

    static bool TryReadInt(Dictionary<string, string> overlays, string key, ref int value)
    {
        if (!overlays.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            return true;
        if (!int.TryParse(raw.Trim(), out int parsed))
            return false;
        value = parsed;
        return true;
    }

    static bool TryReadBool(Dictionary<string, string> overlays, string key, ref bool value)
    {
        if (!overlays.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            return true;

        string token = raw.Trim();
        if (token.Equals("1", StringComparison.OrdinalIgnoreCase)
            || token.Equals("true", StringComparison.OrdinalIgnoreCase)
            || token.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (token.Equals("0", StringComparison.OrdinalIgnoreCase)
            || token.Equals("false", StringComparison.OrdinalIgnoreCase)
            || token.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    static bool TryReadNamedArg(IReadOnlyList<string> args, string name, out string value)
    {
        value = null;
        if (args == null || string.IsNullOrEmpty(name))
            return false;

        string flag = "-" + name;
        string prefix = flag + "=";
        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (string.IsNullOrEmpty(token))
                continue;

            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = token.Substring(prefix.Length);
                return value.Length > 0;
            }

            if (!string.Equals(token, flag, StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 >= args.Count)
                return false;

            string next = args[i + 1];
            if (string.IsNullOrEmpty(next) || next.StartsWith("-", StringComparison.Ordinal))
                return false;
            value = next;
            return true;
        }

        return false;
    }

    /// <summary>统一文件键：去掉连字符与下划线后小写，便于 max-players / maxPlayers 同义。</summary>
    static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var chars = new char[key.Length];
        int write = 0;
        for (int i = 0; i < key.Length; i++)
        {
            char c = key[i];
            if (c == '-' || c == '_' || char.IsWhiteSpace(c))
                continue;
            chars[write++] = char.ToLowerInvariant(c);
        }

        return write == 0 ? string.Empty : new string(chars, 0, write);
    }

    /// <summary>密钥类键只跳过，调用方不得把原值写进日志或异常。</summary>
    static bool IsSecretKey(string normalizedKey)
    {
        return normalizedKey.IndexOf("secret", StringComparison.Ordinal) >= 0
            || normalizedKey.IndexOf("token", StringComparison.Ordinal) >= 0
            || normalizedKey.IndexOf("password", StringComparison.Ordinal) >= 0
            || normalizedKey.IndexOf("auth", StringComparison.Ordinal) >= 0;
    }
}
