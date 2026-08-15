using NUnit.Framework;

/// <summary>远端 FrameHint 是客机序号，不能和权威帧比早晚。</summary>
public sealed class RoomRemoteInputPolicyTests
{
    /// <summary>尚未应用过任何命令时，任意正 Hint 都应写入。</summary>
    [Test]
    public void ShouldApply_FirstCommand_IsAccepted()
    {
        Assert.That(RoomRemoteInputPolicy.ShouldApply(1, 0), Is.True);
        Assert.That(RoomRemoteInputPolicy.ShouldApply(5000, 0), Is.True);
    }

    /// <summary>比已应用更旧的 Hint 视为乱序，丢弃。</summary>
    [Test]
    public void ShouldApply_OlderHint_IsRejected()
    {
        Assert.That(RoomRemoteInputPolicy.ShouldApply(10, 12), Is.False);
    }

    /// <summary>同等 Hint 是冗余重发，不得再写入以免 Attack 边沿打到下一帧。</summary>
    [Test]
    public void ShouldApply_SameHint_IsRejected()
    {
        Assert.That(RoomRemoteInputPolicy.ShouldApply(12, 12), Is.False);
        Assert.That(RoomRemoteInputPolicy.ShouldApply(0, 0), Is.False);
    }

    /// <summary>更新的 Hint 写入下一权威帧。</summary>
    [Test]
    public void ShouldApply_NewerHint_IsAccepted()
    {
        Assert.That(RoomRemoteInputPolicy.ShouldApply(13, 12), Is.True);
    }
}
