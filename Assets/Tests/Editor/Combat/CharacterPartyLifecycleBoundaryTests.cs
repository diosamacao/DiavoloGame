using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>守卫 Party 生命周期单一所有权，禁止职责重新泄漏回 CharacterActor。</summary>
public sealed class CharacterPartyLifecycleBoundaryTests
{
    /// <summary>CharacterActor 只暴露生命周期对象，不再保留旧状态字段或转发方法。</summary>
    [Test]
    public void CharacterActor_DoesNotRetainLegacyPartyLifecyclePath()
    {
        string source = ReadScript("Domain/Character/CharacterActor.cs");

        Assert.That(
            source,
            Does.Contain("public CharacterPartyLifecycle PartyLifecycle => _partyLifecycle;"));
        Assert.That(source, Does.Not.Contain("_partyState"));
        Assert.That(source, Does.Not.Contain("_queuedExternalIntent"));
        Assert.That(source, Does.Not.Contain("public void SetPartyState("));
        Assert.That(source, Does.Not.Contain("public void BeginPartyExit("));
        Assert.That(source, Does.Not.Contain("public void QueueExternalIntent("));
    }

    /// <summary>Party 状态、退场和支援队列必须集中在 CharacterPartyLifecycle。</summary>
    [Test]
    public void CharacterPartyLifecycle_OwnsRequiredResponsibilities()
    {
        string source = ReadScript(
            "Domain/Character/Party/CharacterPartyLifecycle.cs");

        Assert.That(source, Does.Contain("PartyMemberState _state"));
        Assert.That(source, Does.Contain("GameplayIntentType _queuedExternalIntent"));
        Assert.That(source, Does.Contain("public void BeginExit("));
        Assert.That(source, Does.Contain("internal void AdvanceAfterPostCombat("));
        Assert.That(source, Does.Contain("public void NotifyAssistParryContact("));
        Assert.That(source, Does.Contain("public void PlaceForNormalSwitchFrom("));
    }

    /// <summary>读取 Assets/Scripts 下的生产脚本。</summary>
    static string ReadScript(string relativePath) =>
        File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relativePath));
}
