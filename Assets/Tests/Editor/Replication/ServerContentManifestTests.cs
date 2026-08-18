using NUnit.Framework;

/// <summary>Gameplay 指纹只随玩法身份变化，不把 VFX 名算进去。</summary>
public sealed class ServerContentManifestTests
{
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
}
