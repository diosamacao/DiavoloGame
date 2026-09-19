using NUnit.Framework;

/// <summary>验证结构审计规则只命中真实源码，不被注释或字符串说明误触发。</summary>
public sealed class StructureAuditRuleSetTests
{
    /// <summary>运行时场景查找必须被报告，Editor 路径允许使用同一 API。</summary>
    [Test]
    public void AuditSource_RuntimeFind_IsRejectedOutsideEditor()
    {
        const string source =
            "public sealed class Sample { void Run() { FindObjectOfType<PlayerController>(); } }";

        string[] runtimeIssues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/App/Controllers/Sample.cs",
            source);
        string[] editorIssues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/Editor/Sample.cs",
            source);

        Assert.That(runtimeIssues, Has.Some.Contains("场景 Find API"));
        Assert.That(editorIssues, Has.None.Contains("场景 Find API"));
    }

    /// <summary>Domain 的 Architecture Singleton 与 Resources 配置查找均必须失败。</summary>
    [Test]
    public void AuditSource_DomainServiceLocators_AreRejected()
    {
        const string source = @"
public sealed class Sample : AppControllerBase
{
    void Run()
    {
        var app = ACTGameArchitecture.Interface;
        var config = Resources.Load<object>(""Config"");
    }
}";

        string[] issues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/Domain/Sample.cs",
            source);

        Assert.That(issues, Has.Some.Contains("ACTGameArchitecture.Interface"));
        Assert.That(issues, Has.Some.Contains("Resources.Load"));
        Assert.That(issues, Has.Some.Contains("App Architecture"));
    }

    /// <summary>空 catch 与只 return/continue 的 catch 都属于不可观测异常吞噬。</summary>
    [TestCase("try { Run(); } catch (System.Exception) { }")]
    [TestCase("try { Run(); } catch (System.Exception) { return; }")]
    public void AuditSource_SilentCatch_IsRejected(string source)
    {
        string[] issues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/App/Sample.cs",
            $"public sealed class Sample {{ void Test() {{ {source} }} void Run() {{ }} }}");

        Assert.That(issues, Has.Some.Contains("catch 不得静默吞异常"));
    }

    /// <summary>记录异常的 catch 具备可观测性，不应被空吞规则误报。</summary>
    [Test]
    public void AuditSource_ObservableCatch_IsAccepted()
    {
        const string source = @"
public sealed class Sample
{
    void Run()
    {
        try { Work(); }
        catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
    }
    void Work() { }
}";

        string[] issues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/App/Sample.cs",
            source);

        Assert.That(issues, Has.None.Contains("catch 不得静默吞异常"));
    }

    /// <summary>多个顶层 public 类型与已删除协议符号必须被结构审计阻断。</summary>
    [Test]
    public void AuditSource_PublicTypesAndRemovedProtocol_AreRejected()
    {
        const string source = @"
public sealed class First { ReplicationFrame frame; }
public sealed class Second { }";

        string[] issues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/App/Sample.cs",
            source);

        Assert.That(issues, Has.Some.Contains("一个文件只能声明一个"));
        Assert.That(issues, Has.Some.Contains("ReplicationFrame"));
    }

    /// <summary>注释和字符串中的规范示例不应被当成真实违规。</summary>
    [Test]
    public void AuditSource_CommentsAndStrings_AreIgnored()
    {
        const string source = @"
// FindObjectOfType<PlayerController>();
public sealed class Sample
{
    string text = ""ReplicationFrame Resources.Load<object>()"";
}";

        string[] issues = StructureAuditRuleSet.AuditSource(
            "Assets/Scripts/Domain/Sample.cs",
            source);

        Assert.That(issues, Is.Empty);
    }

    /// <summary>Framework、Domain 与 Runtime 程序集的反向引用必须分别被报告。</summary>
    [TestCase(
        "Assets/Scripts/Framework/ACTNet/Sample.asmdef",
        "ACTNet.Sample",
        "ACTGame.Simulation",
        "Framework 程序集")]
    [TestCase(
        "Assets/Scripts/Domain/Sample/Sample.asmdef",
        "ACTGame.Domain.Sample",
        "ACTGame.App",
        "Domain 程序集")]
    [TestCase(
        "Assets/Scripts/App/Sample.asmdef",
        "ACTGame.App",
        "ACTGame.Tools.Editor",
        "运行时程序集")]
    public void AuditAssemblyDefinition_ReverseDependency_IsRejected(
        string assetPath,
        string assemblyName,
        string reference,
        string expected)
    {
        string json =
            $"{{ \"name\": \"{assemblyName}\", \"references\": [\"{reference}\"] }}";

        string[] issues = StructureAuditRuleSet.AuditAssemblyDefinition(assetPath, json);

        Assert.That(issues, Has.Some.Contains(expected));
    }

    /// <summary>Framework 到 Framework 的单向运行时引用符合结构门禁。</summary>
    [Test]
    public void AuditAssemblyDefinition_AllowedDependency_IsAccepted()
    {
        const string json =
            "{ \"name\": \"ACTNet.Transport\", \"references\": [\"ACTNet.Core\"] }";

        string[] issues = StructureAuditRuleSet.AuditAssemblyDefinition(
            "Assets/Scripts/Framework/ACTNet/Transport/ACTNet.Transport.asmdef",
            json);

        Assert.That(issues, Is.Empty);
    }

    /// <summary>已登记程序集引用白名单外的同层依赖也必须拒绝，不能只检查反向引用。</summary>
    [Test]
    public void AuditAssemblyDefinition_UnlistedDependency_IsRejected()
    {
        const string json =
            "{ \"name\": \"ACTNet.Transport\", \"references\": [\"ACTNet.Session\"] }";

        string[] issues = StructureAuditRuleSet.AuditAssemblyDefinition(
            "Assets/Scripts/Framework/ACTNet/Transport/ACTNet.Transport.asmdef",
            json);

        Assert.That(issues, Has.Some.Contains("不允许引用 ACTNet.Session"));
    }

    /// <summary>新增生产程序集未登记边界策略时必须失败，防止默认放行未知模块。</summary>
    [Test]
    public void AuditAssemblyDefinition_UnknownAssembly_IsRejected()
    {
        const string json =
            "{ \"name\": \"ACTGame.Domain.Unknown\", \"references\": [] }";

        string[] issues = StructureAuditRuleSet.AuditAssemblyDefinition(
            "Assets/Scripts/Domain/Unknown/ACTGame.Domain.Unknown.asmdef",
            json);

        Assert.That(issues, Has.Some.Contains("未登记引用白名单"));
    }

    /// <summary>超阈值运行时文件必须拆分或登记职责，已登记聚合根不误报。</summary>
    [Test]
    public void AuditLargeRuntimeFile_RequiresDocumentedResponsibility()
    {
        string oversized = new string('\n', 451);

        string unknown = StructureAuditRuleSet.AuditLargeRuntimeFile(
            "Assets/Scripts/App/UnknownLargeType.cs",
            oversized);
        string documented = StructureAuditRuleSet.AuditLargeRuntimeFile(
            "Assets/Scripts/Domain/Character/CharacterActor.cs",
            oversized);

        Assert.That(unknown, Does.Contain("超过 450 行"));
        Assert.That(documented, Is.Empty);
    }
}
