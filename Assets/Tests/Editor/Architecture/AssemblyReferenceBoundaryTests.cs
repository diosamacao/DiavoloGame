using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证生产 asmdef 名称唯一且依赖方向符合 Framework、Domain、App 分层。</summary>
public sealed class AssemblyReferenceBoundaryTests
{
    /// <summary>全部生产 asmdef 必须通过与 BatchMode 共用的方向规则。</summary>
    [Test]
    public void ProductionAssemblyDefinitions_HaveAllowedDirections()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
        string[] files =
            Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories);

        Assert.That(files.Length, Is.GreaterThan(0));
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = ToAssetPath(files[i]);
            string[] issues = StructureAuditRuleSet.AuditAssemblyDefinition(
                assetPath,
                File.ReadAllText(files[i]));
            Assert.That(issues, Is.Empty, assetPath);
        }
    }

    /// <summary>程序集名必须唯一，避免 Unity 按名称解析到错误模块。</summary>
    [Test]
    public void ProductionAssemblyDefinitions_HaveUniqueNames()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
        string[] files =
            Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories);
        var ownersByName = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < files.Length; i++)
        {
            string json = File.ReadAllText(files[i]);
            Match match = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
            Assert.That(match.Success, Is.True, files[i]);

            string name = match.Groups[1].Value;
            Assert.That(
                ownersByName.TryGetValue(name, out string existing),
                Is.False,
                $"{name}: {existing} / {files[i]}");
            ownersByName.Add(name, files[i]);
        }
    }

    /// <summary>纯模拟程序集必须继续禁用 UnityEngine 引用。</summary>
    [Test]
    public void SimulationAssembly_RemainsEngineIndependent()
    {
        string path = Path.Combine(
            Application.dataPath,
            "Scripts",
            "Domain",
            "Simulation",
            "ACTGame.Simulation.asmdef");
        string json = File.ReadAllText(path);

        Assert.That(json, Does.Contain(@"""noEngineReferences"": true"));
    }

    /// <summary>Editor 之外的运行时脚本必须由显式 asmdef 接管，不得回落 Assembly-CSharp。</summary>
    [Test]
    public void RuntimeSources_AreOwnedByExplicitAssemblies()
    {
        // Application.dataPath 在 Windows 仍使用 /，DirectoryInfo.FullName 使用本机分隔符。
        string scriptsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts"));
        string editorRoot = Path.Combine(scriptsRoot, "Editor")
            + Path.DirectorySeparatorChar;
        string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].StartsWith(editorRoot, StringComparison.OrdinalIgnoreCase))
                continue;
            Assert.That(HasOwningAssembly(files[i], scriptsRoot), Is.True, files[i]);
        }
    }

    /// <summary>沿父目录查找最近 asmdef，直到离开 Assets/Scripts。</summary>
    static bool HasOwningAssembly(string sourceFile, string scriptsRoot)
    {
        scriptsRoot = Path.GetFullPath(scriptsRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        DirectoryInfo directory = new FileInfo(sourceFile).Directory;
        while (directory != null
            && (directory.FullName + Path.DirectorySeparatorChar).StartsWith(
                scriptsRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            if (directory.GetFiles("*.asmdef", SearchOption.TopDirectoryOnly).Length > 0)
                return true;
            directory = directory.Parent;
        }

        return false;
    }

    /// <summary>把生产绝对路径转换为结构审计使用的 Assets 相对路径。</summary>
    static string ToAssetPath(string fullPath)
    {
        string projectRoot =
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Substring(projectRoot.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
    }
}
