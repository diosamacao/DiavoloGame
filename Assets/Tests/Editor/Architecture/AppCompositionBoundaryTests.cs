using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定 CS4 App Composition：Server Runtime 不解释角色，Controller 不扫描场景业务依赖。</summary>
public sealed class AppCompositionBoundaryTests
{
    /// <summary>Dedicated Runtime 只处理 Session/Match/Poll/Flush，不得引用具体角色语义。</summary>
    [Test]
    public void DedicatedServerRuntime_DoesNotInterpretCharacterState()
    {
        string source = ReadScript("App/Server/DedicatedServerRuntime.cs");
        string[] forbidden =
        {
            "CharacterActor",
            "CharacterStateType",
            "PartyMemberState",
            "ActionDefinition",
            "AnimationKey",
            "HitReactionKind",
        };

        for (int i = 0; i < forbidden.Length; i++)
            Assert.That(source, Does.Not.Contain(forbidden[i]), forbidden[i]);
    }

    /// <summary>运行时 Controller 必须从 Composition Root、同物体或注册表取依赖。</summary>
    [Test]
    public void RuntimeControllers_DoNotUseSceneFindApis()
    {
        string root = Path.Combine(Application.dataPath, "Scripts", "App", "Controllers");
        string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        var findApi = new Regex(
            @"\b(?:Object\.)?(?:FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType)\s*<");

        Assert.That(files.Length, Is.GreaterThan(0));
        for (int i = 0; i < files.Length; i++)
        {
            string source = StripComments(File.ReadAllText(files[i]));
            Assert.That(findApi.IsMatch(source), Is.False, files[i]);
        }
    }

    /// <summary>从 Assets 相对路径读取生产脚本。</summary>
    static string ReadScript(string relativePath) =>
        File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relativePath));

    /// <summary>移除注释，避免规范文字中的 API 名称触发误报。</summary>
    static string StripComments(string source)
    {
        string withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }
}
