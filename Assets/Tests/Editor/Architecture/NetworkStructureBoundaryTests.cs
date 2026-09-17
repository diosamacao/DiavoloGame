using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>冻结 Replication V2 单轨与统一 Domain/Networking 身份目录。</summary>
public sealed class NetworkStructureBoundaryTests
{
    static readonly string[] s_removedRuntimeSymbols =
    {
        "ReplicationFrame",
        "CharacterSnapshotSchemaV1",
        "ActReplicationApplicationPayload",
        "RoomMessageKind",
    };

    /// <summary>旧 Domain/Net 程序集与目录不得重新出现。</summary>
    [Test]
    public void LegacyDomainNetAssembly_IsRemoved()
    {
        string domainRoot = Path.Combine(Application.dataPath, "Scripts", "Domain");

        string legacyDirectory = Path.Combine(domainRoot, "Net");
        if (Directory.Exists(legacyDirectory))
        {
            Assert.That(
                Directory.GetFiles(legacyDirectory, "*", SearchOption.AllDirectories),
                Is.Empty);
        }
        Assert.That(
            File.Exists(Path.Combine(legacyDirectory, "ACTGame.Net.asmdef")),
            Is.False);
        Assert.That(
            File.Exists(
                Path.Combine(
                    domainRoot,
                    "Networking",
                    "Identity",
                    "SimActorNetIdAdapter.cs")),
            Is.True);
    }

    /// <summary>除 Editor 审计规则外，运行时生产脚本不得引用已删除协议符号。</summary>
    [Test]
    public void RuntimeSources_DoNotReferenceRemovedReplicationProtocol()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
        string editorRoot = Path.Combine(scriptsRoot, "Editor") + Path.DirectorySeparatorChar;
        string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            string file = files[fileIndex];
            if (file.StartsWith(editorRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            string source = File.ReadAllText(file);
            for (int symbolIndex = 0; symbolIndex < s_removedRuntimeSymbols.Length; symbolIndex++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(s_removedRuntimeSymbols[symbolIndex]),
                    file);
            }
        }
    }
}
