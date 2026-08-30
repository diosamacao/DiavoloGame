using NUnit.Framework;
using UnityEngine;

/// <summary>CameraDirectorStack 的优先级、去重与恢复行为测试。</summary>
public sealed class CameraDirectorStackTests
{
    /// <summary>高优 SkillShot 覆盖 LockOn，移除后恢复 LockOn。</summary>
    [Test]
    public void SkillShot_Remove_RestoresPreviousGameplayMode()
    {
        var stack = new CameraDirectorStack();
        stack.Push(CameraMode.LockOn, 20);
        stack.Push(CameraMode.SkillShot, 80);

        Assert.That(stack.Active.Mode, Is.EqualTo(CameraMode.SkillShot));

        stack.Remove(CameraMode.SkillShot);

        Assert.That(stack.Active.Mode, Is.EqualTo(CameraMode.LockOn));
    }

    /// <summary>同模式重复 Push 只保留最新优先级。</summary>
    [Test]
    public void Push_SameMode_ReplacesExistingEntry()
    {
        var stack = new CameraDirectorStack();
        stack.Push(CameraMode.SkillShot, 80);
        stack.Push(CameraMode.SkillShot, 90);

        Assert.That(stack.Active.Mode, Is.EqualTo(CameraMode.SkillShot));
        Assert.That(stack.Active.Priority, Is.EqualTo(90));
    }

    /// <summary>FollowHold 解除后即使角色已冲出 3m，也从钉点平滑追回而非瞬移。</summary>
    [Test]
    public void CameraRig_ClearHold_RecoversWithoutSnap()
    {
        var owner = new GameObject("CameraRigTest");
        var root = new GameObject("Root").transform;
        var orbit = new GameObject("Orbit").transform;
        var pitch = new GameObject("Pitch").transform;
        try
        {
            var rig = owner.AddComponent<CameraRig>();
            rig.Bind(root, orbit, pitch);
            rig.Sync(root, 0f, 0f, 0.1f, 1f, Vector3.forward);
            rig.SetFollowHold();
            root.position = new Vector3(10f, 0f, 0f);
            rig.Sync(root, 0f, 0f, 0.1f, 1f, Vector3.forward);
            rig.ClearFollowHold();
            rig.Sync(root, 0f, 0f, 0.1f, 1f, Vector3.forward);

            Assert.That(rig.FollowAnchorPosition.x, Is.LessThan(10f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(root.gameObject);
            Object.DestroyImmediate(orbit.gameObject);
            Object.DestroyImmediate(pitch.gameObject);
        }
    }
}
