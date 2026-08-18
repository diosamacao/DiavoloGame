using NUnit.Framework;

/// <summary>Headless 动画后端不得创建 Graph 或推进 Clip。</summary>
public sealed class NullAnimationPlaybackTests
{
    /// <summary>Play / Tick / Seek 均为空操作，且 IsValid 为 false。</summary>
    [Test]
    public void NullPlayback_RemainsInvalid()
    {
        var playback = new NullAnimationPlayback();
        playback.Play(null, 0.1f);
        playback.Seek(0.5f);
        playback.Tick(1f / 60f);

        Assert.That(playback.IsValid, Is.False);
        Assert.That(playback.CurrentClip, Is.Null);
        Assert.That(playback.NormalizedTime, Is.EqualTo(0f));
        playback.Dispose();
    }
}
