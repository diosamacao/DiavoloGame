using NUnit.Framework;

/// <summary>Gameplay 指纹只随玩法身份变化，不把 VFX 名算进去。</summary>
public sealed class ServerContentManifestTests
{
    [Test]
    public void ComputeFingerprint_BodyPolicyOrSkinChange_ChangesHash()
    {
        ContentFingerprint a = ServerContentManifest.ComputeFingerprint(1, "bake", new[] { 1 }, new[] { 2 },
            System.Array.Empty<string>(), new[] { "2:1:20" });
        ContentFingerprint b = ServerContentManifest.ComputeFingerprint(1, "bake", new[] { 1 }, new[] { 2 },
            System.Array.Empty<string>(), new[] { "2:0:20" });
        ContentFingerprint c = ServerContentManifest.ComputeFingerprint(1, "bake", new[] { 1 }, new[] { 2 },
            System.Array.Empty<string>(), new[] { "2:1:50" });
        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a, Is.Not.EqualTo(c));
    }

    /// <summary>改动作 Id 集合必须改变指纹。</summary>
    [Test]
    public void ComputeFingerprint_ActionIdsChange_ChangesHash()
    {
        ContentFingerprint a = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 10, 20 },
            new[] { 1, 2 });
        ContentFingerprint b = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 10, 20 },
            new[] { 1, 3 });

        Assert.That(a.IsValid, Is.True);
        Assert.That(a, Is.Not.EqualTo(b));
    }

    /// <summary>相同玩法输入顺序不同仍得到同一指纹。</summary>
    [Test]
    public void ComputeFingerprint_IgnoresInputOrder()
    {
        ContentFingerprint a = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 20, 10 },
            new[] { 2, 1 });
        ContentFingerprint b = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 10, 20 },
            new[] { 1, 2 });

        Assert.That(a, Is.EqualTo(b));
    }

    /// <summary>同一 InputFrame 使用不同确定性意图规则时必须拒绝互联。</summary>
    [Test]
    public void ComputeFingerprint_IntentRulesChange_ChangesHash()
    {
        ContentFingerprint a = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 10 },
            new[] { 1 },
            new[] { "buffer:9", "0:1:0:1:0:21:0" });
        ContentFingerprint b = ServerContentManifest.ComputeFingerprint(
            1,
            "bake",
            new[] { 10 },
            new[] { 1 },
            new[] { "buffer:12", "0:1:0:1:0:21:0" });

        Assert.That(a, Is.Not.EqualTo(b));
    }
}
