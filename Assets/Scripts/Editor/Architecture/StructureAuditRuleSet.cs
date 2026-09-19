using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>对生产 C# 源码执行不依赖 Unity 编译结果的结构债务审计规则。</summary>
public static class StructureAuditRuleSet
{
    const int LargeRuntimeTypeLineThreshold = 450;

    static readonly string[] s_removedProtocolSymbols =
    {
        "ReplicationFrame",
        "ReplicationFrameCodec",
        "CharacterSnapshotSchemaV1",
        "ActReplicationApplicationPayload",
        "RoomMessageKind",
    };

    // 超阈值文件必须登记仍保持单职责的理由；该表不是规则豁免，源码规则仍完整执行。
    static readonly Dictionary<string, string> s_largeRuntimeTypeResponsibilities =
        new(StringComparer.Ordinal)
        {
            ["Assets/Scripts/Domain/Character/CharacterActor.cs"] =
                "角色聚合根公开门面；具体模拟、Party、Prediction 与表现职责均已下放。",
            ["Assets/Scripts/Domain/Character/Locomotion/LocomotionContext.cs"] =
                "Locomotion 状态机共享数据契约；集中持有相位输入、只读服务和单步输出，不执行状态算法。",
            ["Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs"] =
                "SerializeReference 节点 Schema 集合；同文件保持稳定类型身份，不含 Runner 逻辑。",
            ["Assets/Scripts/Domain/Combat/Actions/Definitions/ActionDefinition.cs"] =
                "动作内容聚合 SO；集中序列化播放、Timeline、Motion 与执行策略，只负责内容查询/校验。",
            ["Assets/Scripts/App/Presentation/RemoteCharacterProxy.cs"] =
                "Observer 单一播放头：原子应用复制快照并解释动作、Locomotion 与 Notify。",
            ["Assets/Scripts/App/Controllers/Camera/CameraManager.cs"] =
                "场景相机唯一 Controller；集中协调模式栈、目标绑定与渲染更新。",
            ["Assets/Scripts/App/Server/DedicatedServerRuntime.cs"] =
                "Dedicated 会话宿主门面；Authority Gameplay 已下放，只协调 Session、Match、Poll 与 Flush。",
            ["Assets/Scripts/Framework/ACTNet/Transport/ChannelMuxTransport.cs"] =
                "传输通道复用器；在单一连接边界内集中可靠队列、ACK、重传和通道调度。",
        };

    // 每个生产程序集只可引用登记集合；新增程序集必须先明确其层级和依赖方向。
    static readonly Dictionary<string, HashSet<string>> s_allowedAssemblyReferences =
        new(StringComparer.Ordinal)
        {
            ["ACTNet.Core"] = Allow(),
            ["ACTNet.Transport"] = Allow("ACTNet.Core"),
            ["ACTNet.Session"] = Allow("ACTNet.Core", "ACTNet.Transport"),
            ["ACTNet.Prediction"] = Allow("ACTNet.Core"),
            ["ACTNet.Replication"] = Allow("ACTNet.Core"),
            ["ACTGame.Core"] = Allow(),
            ["ACTGame.Simulation"] = Allow("ACTNet.Core", "ACTNet.Prediction"),
            ["ACTGame.Combat.Resources"] = Allow("ACTGame.Simulation"),
            ["ACTGame.Combat.Numeric"] =
                Allow("ACTGame.Simulation", "ACTGame.Combat.Resources"),
            ["ACTGame.Networking"] =
                Allow("ACTGame.Simulation", "ACTNet.Core", "ACTNet.Replication"),
            ["ACTGame.Domain.Input"] =
                Allow("ACTGame.Simulation", "Unity.InputSystem"),
            ["ACTGame.Domain.Combat"] = Allow(
                "ACTGame.Core",
                "ACTGame.Domain.Input",
                "ACTGame.Simulation",
                "ACTGame.Combat.Numeric",
                "ACTGame.Combat.Resources",
                "Cinemachine",
                "Unity.Mathematics",
                "Unity.Splines"),
            ["ACTGame.Domain.Character"] = Allow(
                "ACTGame.Core",
                "ACTGame.Domain.Input",
                "ACTGame.Domain.Combat",
                "ACTGame.Simulation",
                "ACTGame.Combat.Numeric",
                "ACTGame.Combat.Resources",
                "ACTGame.Networking",
                "ACTNet.Core",
                "ACTNet.Prediction",
                "ACTNet.Replication",
                "Unity.InputSystem"),
            ["ACTGame.Domain.Enemy"] = Allow(
                "ACTGame.Domain.Input",
                "ACTGame.Domain.Combat",
                "ACTGame.Domain.Character",
                "ACTGame.Simulation",
                "ACTGame.Combat.Numeric"),
            ["ACTGame.Infrastructure"] =
                Allow("ACTGame.Simulation", "ACTGame.Domain.Input", "Unity.InputSystem"),
            ["ACTGame.Server"] = Allow(
                "ACTNet.Core",
                "ACTNet.Transport",
                "ACTNet.Session",
                "ACTNet.Replication",
                "ACTGame.Simulation"),
            ["ACTGame.App"] = Allow(
                "ACTGame.Core",
                "ACTGame.Domain.Input",
                "ACTGame.Domain.Combat",
                "ACTGame.Domain.Character",
                "ACTGame.Domain.Enemy",
                "ACTGame.Simulation",
                "ACTGame.Networking",
                "ACTGame.Combat.Numeric",
                "ACTGame.Combat.Resources",
                "ACTGame.Infrastructure",
                "ACTGame.Server",
                "ACTNet.Core",
                "ACTNet.Transport",
                "ACTNet.Session",
                "ACTNet.Replication",
                "ACTNet.Prediction",
                "Cinemachine",
                "Unity.InputSystem"),
            ["ACTGame.Previews"] = Allow(),
        };

    /// <summary>扫描 Assets/Scripts 下全部生产脚本并返回稳定排序的问题描述。</summary>
    public static string[] AuditProject(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));

        string scriptsRoot = Path.Combine(projectRoot, "Assets", "Scripts");
        if (!Directory.Exists(scriptsRoot))
            throw new DirectoryNotFoundException($"生产脚本目录不存在：{scriptsRoot}");

        var issues = new List<string>();
        string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = ToAssetPath(projectRoot, files[i]);
            string source = File.ReadAllText(files[i]);
            string[] sourceIssues = AuditSource(assetPath, source);
            for (int issue = 0; issue < sourceIssues.Length; issue++)
                issues.Add($"{assetPath}: {sourceIssues[issue]}");

            string largeTypeIssue = AuditLargeRuntimeFile(assetPath, source);
            if (!string.IsNullOrEmpty(largeTypeIssue))
                issues.Add($"{assetPath}: {largeTypeIssue}");
        }

        string[] assemblyFiles =
            Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories);
        Array.Sort(assemblyFiles, StringComparer.Ordinal);
        for (int i = 0; i < assemblyFiles.Length; i++)
        {
            string assetPath = ToAssetPath(projectRoot, assemblyFiles[i]);
            string[] assemblyIssues =
                AuditAssemblyDefinition(assetPath, File.ReadAllText(assemblyFiles[i]));
            for (int issue = 0; issue < assemblyIssues.Length; issue++)
                issues.Add($"{assetPath}: {assemblyIssues[issue]}");
        }

        issues.Sort(StringComparer.Ordinal);
        return issues.ToArray();
    }

    /// <summary>审计单份源码；供 Editor 菜单和无 Unity 对象的 EditMode 测试共用。</summary>
    public static string[] AuditSource(string assetPath, string source)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            throw new ArgumentException("Asset 路径不能为空。", nameof(assetPath));

        string normalizedPath = assetPath.Replace('\\', '/');
        string code = StripCommentsAndStrings(source ?? string.Empty);
        var issues = new List<string>();

        if (IsProductionRuntimePath(normalizedPath))
        {
            AddIfMatches(
                issues,
                code,
                @"\b(?:FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType)\s*<",
                "运行时代码禁止使用场景 Find API；依赖必须由 Composition Root 注入。");
        }

        if (normalizedPath.StartsWith("Assets/Scripts/Domain/", StringComparison.Ordinal))
        {
            AddIfMatches(
                issues,
                code,
                @"\bACTGameArchitecture\s*\.\s*Interface\b",
                "Domain 禁止访问 ACTGameArchitecture.Interface。");
            AddIfMatches(
                issues,
                code,
                @"\bResources\s*\.\s*Load\s*<",
                "Domain 禁止自行 Resources.Load；配置必须由 Content Bootstrap 注入。");
            AddIfMatches(
                issues,
                code,
                @"\b(?:AppControllerBase|ArchitectureSystemBase|IArchitectureController|IArchitectureSystem|IArchitectureEvent)\b",
                "Domain 禁止引用 App Architecture 类型；由 App 单向调用 Domain 端口。");
        }

        AddIfMatches(
            issues,
            code,
            @"catch\s*(?:\([^)]*\))?\s*\{\s*(?:(?:return(?:\s+[^;]+)?|continue)\s*;\s*)?\}",
            "catch 不得静默吞异常；必须转换结果、计数、记录或明确断开。");

        int publicTopLevelTypes = Regex.Matches(
            code,
            @"(?m)^public\s+(?:(?:sealed|abstract|static|readonly|partial)\s+)*(?:class|struct|interface|enum)\s+")
            .Count;
        if (publicTopLevelTypes > 1)
            issues.Add("一个文件只能声明一个顶层 public 类型。");

        if (IsProductionRuntimePath(normalizedPath))
        {
            for (int i = 0; i < s_removedProtocolSymbols.Length; i++)
            {
                string symbol = s_removedProtocolSymbols[i];
                if (Regex.IsMatch(code, $@"\b{Regex.Escape(symbol)}\b"))
                    issues.Add($"生产代码禁止恢复已删除协议符号 {symbol}。");
            }
        }

        issues.Sort(StringComparer.Ordinal);
        return issues.ToArray();
    }

    /// <summary>审计超大运行时文件；已登记明确职责的文件允许保留聚合门面。</summary>
    public static string AuditLargeRuntimeFile(string assetPath, string source)
    {
        string normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
        int lineCount = CountLines(source);
        if (!IsProductionRuntimePath(normalizedPath)
            || lineCount <= LargeRuntimeTypeLineThreshold
            || s_largeRuntimeTypeResponsibilities.ContainsKey(normalizedPath))
        {
            return string.Empty;
        }

        return $"运行时文件 {lineCount} 行，超过 {LargeRuntimeTypeLineThreshold} 行；"
            + "须拆分职责或登记单一职责说明。";
    }

    /// <summary>按逐程序集白名单审计引用，并额外报告跨层反向依赖。</summary>
    public static string[] AuditAssemblyDefinition(string assetPath, string json)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            throw new ArgumentException("Asmdef 路径不能为空。", nameof(assetPath));

        string source = json ?? string.Empty;
        Match nameMatch = Regex.Match(source, @"""name""\s*:\s*""([^""]+)""");
        if (!nameMatch.Success)
            return new[] { "asmdef 缺少有效 name。" };

        string assemblyName = nameMatch.Groups[1].Value;
        Match referencesMatch = Regex.Match(
            source,
            @"""references""\s*:\s*\[(.*?)\]",
            RegexOptions.Singleline);
        MatchCollection references = Regex.Matches(
            referencesMatch.Success ? referencesMatch.Groups[1].Value : string.Empty,
            @"""([^""]+)""");
        var issues = new List<string>();
        bool hasAllowlist =
            s_allowedAssemblyReferences.TryGetValue(assemblyName, out HashSet<string> allowed);
        if (!hasAllowlist)
            issues.Add($"生产程序集 {assemblyName} 未登记引用白名单。");

        for (int i = 0; i < references.Count; i++)
        {
            string reference = references[i].Groups[1].Value;
            if (hasAllowlist && !allowed.Contains(reference))
                issues.Add($"程序集 {assemblyName} 不允许引用 {reference}。");

            if (assemblyName.StartsWith("ACTNet.", StringComparison.Ordinal)
                && reference.StartsWith("ACTGame.", StringComparison.Ordinal))
            {
                issues.Add($"Framework 程序集 {assemblyName} 禁止反向引用 {reference}。");
            }

            if (assetPath.Replace('\\', '/').StartsWith(
                    "Assets/Scripts/Domain/",
                    StringComparison.Ordinal)
                && reference.StartsWith("ACTGame.App", StringComparison.Ordinal))
            {
                issues.Add($"Domain 程序集 {assemblyName} 禁止引用 {reference}。");
            }

            if (!assemblyName.EndsWith(".Editor", StringComparison.Ordinal)
                && reference.EndsWith(".Editor", StringComparison.Ordinal))
            {
                issues.Add($"运行时程序集 {assemblyName} 禁止引用 Editor 程序集 {reference}。");
            }
        }

        issues.Sort(StringComparer.Ordinal);
        return issues.ToArray();
    }

    /// <summary>创建按程序集名精确比较的引用白名单。</summary>
    static HashSet<string> Allow(params string[] references) =>
        new(references ?? Array.Empty<string>(), StringComparer.Ordinal);

    /// <summary>返回当前生产源码是否属于运行时路径；Editor 与 Previews 不参与运行时 Find 门禁。</summary>
    static bool IsProductionRuntimePath(string normalizedPath) =>
        normalizedPath.StartsWith("Assets/Scripts/", StringComparison.Ordinal)
        && !normalizedPath.StartsWith("Assets/Scripts/Editor/", StringComparison.Ordinal)
        && !normalizedPath.StartsWith("Assets/Scripts/Previews/", StringComparison.Ordinal);

    /// <summary>按换行符稳定统计文本行数，空文本为零行。</summary>
    static int CountLines(string source)
    {
        if (string.IsNullOrEmpty(source))
            return 0;

        int lines = 1;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
                lines++;
        }
        return lines;
    }

    /// <summary>正则命中时追加一次问题，避免同文件同规则产生重复噪声。</summary>
    static void AddIfMatches(
        List<string> issues,
        string source,
        string pattern,
        string message)
    {
        if (Regex.IsMatch(source, pattern, RegexOptions.Singleline))
            issues.Add(message);
    }

    /// <summary>剥离注释和普通字符串，避免规范说明文本触发源码规则。</summary>
    static string StripCommentsAndStrings(string source)
    {
        string withoutBlockComments = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline);
        string withoutLineComments = Regex.Replace(
            withoutBlockComments,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline);
        return Regex.Replace(
            withoutLineComments,
            @"""(?:\\.|[^""\\])*""",
            "\"\"");
    }

    /// <summary>把绝对磁盘路径转换为统一正斜杠 Assets 相对路径。</summary>
    static string ToAssetPath(string projectRoot, string fullPath)
    {
        string relative = fullPath.Substring(Path.GetFullPath(projectRoot).TrimEnd('\\', '/').Length)
            .TrimStart('\\', '/');
        return relative.Replace('\\', '/');
    }
}
