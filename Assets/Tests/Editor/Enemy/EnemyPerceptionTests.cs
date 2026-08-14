using NUnit.Framework;
using UnityEngine;

/// <summary>敌人感知从玩家根列表选取水平最近目标。</summary>
public sealed class EnemyPerceptionTests
{
    [Test]
    public void Capture_SelectsClosestPlanarPlayerRoot()
    {
        var self = new GameObject("PerceptionSelf");
        self.transform.position = Vector3.zero;
        var near = new GameObject("NearPlayer");
        near.transform.position = new Vector3(2f, 10f, 0f);
        var far = new GameObject("FarPlayer");
        far.transform.position = new Vector3(8f, 0f, 0f);
        Transform[] roots = { far.transform, near.transform };

        var perception = new EnemyPerception(
            self.transform,
            () => roots,
            () => CharacterStateType.Locomotion,
            () => false);

        EnemyPerceptionSnapshot snapshot = perception.Capture();

        Assert.That(snapshot.HasTarget, Is.True);
        Assert.That(snapshot.TargetPosition.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(snapshot.PlanarDistance, Is.EqualTo(2f).Within(0.001f));

        Object.DestroyImmediate(far);
        Object.DestroyImmediate(near);
        Object.DestroyImmediate(self);
    }

    [Test]
    public void Capture_EmptyList_HasNoTarget()
    {
        var self = new GameObject("PerceptionSelfEmpty");
        Transform[] roots = System.Array.Empty<Transform>();
        var perception = new EnemyPerception(
            self.transform,
            () => roots,
            () => CharacterStateType.Locomotion,
            () => false);

        EnemyPerceptionSnapshot snapshot = perception.Capture();

        Assert.That(snapshot.HasTarget, Is.False);
        Object.DestroyImmediate(self);
    }
}
